using System.Net;
using Ardalis.Result;
using Infrastructure.Services.GitHub.Models;
using Microsoft.Extensions.Logging;
using Refit;

namespace Infrastructure.Services.GitHub;

public interface IGitHubService
{
    // Takes the token explicitly: it is called to validate one, before there is a session holding it.
    public Task<Result<GitHubAccount>> GetAccountAsync(string accessToken, CancellationToken cancellationToken = default);

    public Task<Result<IReadOnlyList<GitHubRepository>>> GetRepositoriesAsync(CancellationToken cancellationToken = default);

    public Task<Result<IReadOnlyList<GitHubWorkflow>>> GetWorkflowsAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default);

    public Task<Result<IReadOnlyList<GitHubRun>>> GetWorkflowRunsAsync(
        string owner,
        string repository,
        string workflowFile,
        int limit,
        CancellationToken cancellationToken = default);

    public Task<Result<IReadOnlyList<GitHubPullRequest>>> GetPullRequestsAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default);

    public Task<Result<GitHubPullRequest>> GetPullRequestAsync(
        string owner,
        string repository,
        int number,
        CancellationToken cancellationToken = default);

    public Task<Result> ClosePullRequestAsync(
        string owner,
        string repository,
        int number,
        CancellationToken cancellationToken = default);

    // Always a squash: the portal merges one commit per pull request and nothing else.
    // The sha is the head the caller decided on; GitHub refuses the merge if it moved since.
    public Task<Result> MergePullRequestAsync(
        string owner,
        string repository,
        int number,
        string sha,
        CancellationToken cancellationToken = default);

    public Task<Result<IReadOnlyList<GitHubCheckRun>>> GetCheckRunsAsync(
        string owner,
        string repository,
        string sha,
        CancellationToken cancellationToken = default);

    public Task<Result<IReadOnlyList<GitHubReview>>> GetReviewsAsync(
        string owner,
        string repository,
        int number,
        CancellationToken cancellationToken = default);
}

internal sealed class GitHubService(IGitHubApi gitHubApi, ILogger<GitHubService> logger) : IGitHubService
{
    private const int MaxRepositoryPages = 5;
    private const int RepositoryPageSize = 100;
    private const int PullRequestsPerRepository = 20;
    private const int CheckRunsPerCommit = 100;
    private const int ReviewsPerPullRequest = 100;
    private const string SquashMerge = "squash";

    public Task<Result<GitHubAccount>> GetAccountAsync(string accessToken, CancellationToken cancellationToken = default) =>
        CallAsync(() => gitHubApi.GetAccountAsync(accessToken, cancellationToken));

    public Task<Result<IReadOnlyList<GitHubRepository>>> GetRepositoriesAsync(CancellationToken cancellationToken = default) =>
        CallAsync<IReadOnlyList<GitHubRepository>>(async () =>
        {
            var repositories = new List<GitHubRepository>();

            for (var page = 1; page <= MaxRepositoryPages; page++)
            {
                var batch = await gitHubApi.GetRepositoriesAsync(page, RepositoryPageSize, cancellationToken);
                repositories.AddRange(batch);

                if (batch.Count < RepositoryPageSize)
                {
                    break;
                }
            }

            return repositories;
        });

    public Task<Result<IReadOnlyList<GitHubWorkflow>>> GetWorkflowsAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default) =>
        CallAsync(async () =>
        {
            var workflows = await gitHubApi.GetWorkflowsAsync(owner, repository, cancellationToken);
            return workflows.Workflows;
        });

    public Task<Result<IReadOnlyList<GitHubRun>>> GetWorkflowRunsAsync(
        string owner,
        string repository,
        string workflowFile,
        int limit,
        CancellationToken cancellationToken = default) =>
        CallAsync<IReadOnlyList<GitHubRun>>(async () =>
        {
            var runs = await gitHubApi.GetWorkflowRunsAsync(owner, repository, workflowFile, limit, cancellationToken);
            return runs.WorkflowRuns.Take(limit).ToList();
        });

    public Task<Result<IReadOnlyList<GitHubPullRequest>>> GetPullRequestsAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default) =>
        CallAsync(() => gitHubApi.GetPullRequestsAsync(owner, repository, PullRequestsPerRepository, cancellationToken));

    public Task<Result<GitHubPullRequest>> GetPullRequestAsync(
        string owner,
        string repository,
        int number,
        CancellationToken cancellationToken = default) =>
        CallAsync(() => gitHubApi.GetPullRequestAsync(owner, repository, number, cancellationToken));

    public Task<Result> ClosePullRequestAsync(
        string owner,
        string repository,
        int number,
        CancellationToken cancellationToken = default) =>
        CallAsync(() => gitHubApi.ClosePullRequestAsync(
            owner,
            repository,
            number,
            new GitHubPullRequestUpdate("closed"),
            cancellationToken));

    public Task<Result> MergePullRequestAsync(
        string owner,
        string repository,
        int number,
        string sha,
        CancellationToken cancellationToken = default) =>
        CallAsync(() => gitHubApi.MergePullRequestAsync(
            owner,
            repository,
            number,
            new GitHubMerge(sha, SquashMerge),
            cancellationToken));

    public Task<Result<IReadOnlyList<GitHubCheckRun>>> GetCheckRunsAsync(
        string owner,
        string repository,
        string sha,
        CancellationToken cancellationToken = default) =>
        CallAsync(async () =>
        {
            var checkRuns = await gitHubApi.GetCheckRunsAsync(owner, repository, sha, CheckRunsPerCommit, cancellationToken);
            return checkRuns.CheckRuns;
        });

    public Task<Result<IReadOnlyList<GitHubReview>>> GetReviewsAsync(
        string owner,
        string repository,
        int number,
        CancellationToken cancellationToken = default) =>
        CallAsync(() => gitHubApi.GetReviewsAsync(owner, repository, number, ReviewsPerPullRequest, cancellationToken));

    // GitHub refusing a write is an outcome the user sees, not an incident: 405 and 409 are
    // how it answers a merge it will not do — conflicts, protection rules, a head that moved.
    private async Task<Result> CallAsync(Func<Task> call)
    {
        try
        {
            await call();

            return Result.Success();
        }
        catch (ApiException exception)
            when (exception.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.Conflict)
        {
            logger.LogWarning(exception, "GitHub refused the write");

            return Result.Conflict(GitHubErrors.Refused);
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TimeoutException)
        {
            logger.LogWarning(exception, "GitHub call failed");

            return Result.Error(GitHubErrors.RequestFailed);
        }
    }

    // GitHub answering with a failure is an outcome the dashboard shows, not an incident,
    // so the Refit exception is turned into a Result rather than escaping the boundary.
    private async Task<Result<T>> CallAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return Result<T>.Success(await call());
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or TimeoutException)
        {
            logger.LogWarning(exception, "GitHub call failed");

            return Result<T>.Error(GitHubErrors.RequestFailed);
        }
    }
}
