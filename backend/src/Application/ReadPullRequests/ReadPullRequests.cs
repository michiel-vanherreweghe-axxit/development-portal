using Application.Common;
using Domain.Watches;
using Infrastructure.Database;
using Infrastructure.Services.GitHub;
using Infrastructure.Services.GitHub.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.ReadPullRequests;

public sealed class ReadPullRequests(ApplicationDbContext context, IGitHubService gitHubService, CurrentUser currentUser)
{
    private const int MaxParallelRepositories = 4;
    private const int MaxParallelPullRequests = 8;

    public async Task<List<PullRequestDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var gitHubId = currentUser.GitHubId;

        // The pair travels anonymous: a database cannot do DISTINCT over a type of ours, so the
        // rows only become RepositoryKey once they are here.
        var rows = await context.Watches
            .AsNoTracking()
            .Where(watch => context.Users.Any(user => user.Id == watch.UserId && user.GitHubId == gitHubId))
            .Select(watch => new { watch.Owner, watch.Repository })
            .Distinct()
            .OrderBy(repository => repository.Owner)
            .ThenBy(repository => repository.Repository)
            .ToListAsync(cancellationToken);

        var repositories = rows.Select(row => new RepositoryKey(row.Owner, row.Repository)).ToList();

        var perRepository = await Throttled.MapAsync(
            repositories,
            MaxParallelRepositories,
            repository => ReadRepositoryAsync(repository, cancellationToken));

        var pullRequestRows = perRepository.SelectMany(pullRequests => pullRequests).ToList();

        var detailed = await Throttled.MapAsync(
            pullRequestRows,
            MaxParallelPullRequests,
            row => AddDetailAsync(row, cancellationToken));

        return detailed.ToList();
    }

    private async Task<IReadOnlyList<PullRequestRow>> ReadRepositoryAsync(
        RepositoryKey repository,
        CancellationToken cancellationToken)
    {
        var pullRequests = await gitHubService.GetPullRequestsAsync(repository.Owner, repository.Name, cancellationToken);

        return !pullRequests.IsSuccess
            ? [new PullRequestRow(repository, null, pullRequests.Errors.FirstOrDefault())]
            : pullRequests.Value
                .Select(pullRequest => new PullRequestRow(repository, pullRequest, null))
                .ToList();
    }

    private async Task<PullRequestDto> AddDetailAsync(PullRequestRow row, CancellationToken cancellationToken)
    {
        if (row.PullRequest is not { } pullRequest)
        {
            return new PullRequestDto { Repository = row.Repository.FullName, Error = row.Error };
        }

        // A pull request still lists with whatever detail could be read for it.
        var checkRuns = await gitHubService.GetCheckRunsAsync(
            row.Repository.Owner,
            row.Repository.Name,
            pullRequest.Head.Sha,
            cancellationToken);

        var reviews = await gitHubService.GetReviewsAsync(
            row.Repository.Owner,
            row.Repository.Name,
            pullRequest.Number,
            cancellationToken);

        return GitHubMapper.ToDto(pullRequest, row.Repository.FullName) with
        {
            Checks = checkRuns.IsSuccess ? checkRuns.Value.Select(GitHubMapper.ToDto).ToList() : [],
            ReviewState = reviews.IsSuccess ? GitHubMapper.ToReviewState(reviews.Value) : ReviewState.ReviewRequired,
        };
    }

    private sealed record RepositoryKey(string Owner, string Name)
    {
        public string FullName => $"{Owner}/{Name}";
    }

    private sealed record PullRequestRow(RepositoryKey Repository, GitHubPullRequest? PullRequest, string? Error);
}
