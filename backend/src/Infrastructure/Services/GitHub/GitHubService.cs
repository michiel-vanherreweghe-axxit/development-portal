using System.Net;
using Ardalis.Result;
using Polly;
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
    private const int MaxPages = 5;
    private const int PageSize = 100;
    private const int PullRequestsPerRepository = 20;
    private const string SquashMerge = "squash";

    // A token GitHub rejects is the user's problem to fix; anything else is ours, and the two
    // must not reach the sign-in page as the same message.
    public async Task<Result<GitHubAccount>> GetAccountAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Result<GitHubAccount>.Success(await gitHubApi.GetAccountAsync(accessToken, cancellationToken));
        }
        catch (ApiException exception)
            when (exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return Result<GitHubAccount>.Unauthorized(GitHubErrors.TokenRejected);
        }
        catch (Exception exception) when (IsCallFailure(exception))
        {
            logger.LogWarning(exception, "GitHub call failed");

            return Result<GitHubAccount>.Error(GitHubErrors.RequestFailed);
        }
    }

    public Task<Result<IReadOnlyList<GitHubRepository>>> GetRepositoriesAsync(CancellationToken cancellationToken = default) =>
        CallAsync<IReadOnlyList<GitHubRepository>>(() =>
            PagedAsync(page => gitHubApi.GetRepositoriesAsync(page, PageSize, cancellationToken)));

    public Task<Result<IReadOnlyList<GitHubWorkflow>>> GetWorkflowsAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default) =>
        CallAsync<IReadOnlyList<GitHubWorkflow>>(() =>
            PagedAsync(async page =>
                (await gitHubApi.GetWorkflowsAsync(owner, repository, page, PageSize, cancellationToken)).Workflows));

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
        // Every page of them: the merge gate reads this list, so a failing check must not fall
        // off the end of it.
        CallAsync<IReadOnlyList<GitHubCheckRun>>(() =>
            PagedAsync(async page =>
                (await gitHubApi.GetCheckRunsAsync(owner, repository, sha, page, PageSize, cancellationToken)).CheckRuns));

    public Task<Result<IReadOnlyList<GitHubReview>>> GetReviewsAsync(
        string owner,
        string repository,
        int number,
        CancellationToken cancellationToken = default) =>
        CallAsync<IReadOnlyList<GitHubReview>>(() =>
            PagedAsync(page => gitHubApi.GetReviewsAsync(owner, repository, number, page, PageSize, cancellationToken)));

    private static async Task<IReadOnlyList<T>> PagedAsync<T>(Func<int, Task<IReadOnlyList<T>>> readPage)
    {
        var items = new List<T>();

        for (var page = 1; page <= MaxPages; page++)
        {
            var batch = await readPage(page);
            items.AddRange(batch);

            if (batch.Count < PageSize)
            {
                break;
            }
        }

        return items;
    }

    // Polly answers a timeout or an open circuit with its own exception type, which derives from
    // none of the http ones; without this the whole dashboard fails on one slow repository.
    private static bool IsCallFailure(Exception exception) =>
        exception is ApiException or HttpRequestException or TimeoutException or ExecutionRejectedException;

    // GitHub refusing a write is an outcome the user sees, not an incident: 403, 405 and 409 are
    // how it answers a write it will not do — a token without write access, conflicts, protection
    // rules, a head that moved.
    private async Task<Result> CallAsync(Func<Task> call)
    {
        try
        {
            await call();

            return Result.Success();
        }
        catch (ApiException exception)
            when (exception.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.MethodNotAllowed
                or HttpStatusCode.Conflict)
        {
            logger.LogWarning(exception, "GitHub refused the write");

            return Result.Conflict(GitHubErrors.Refused);
        }
        catch (Exception exception) when (IsCallFailure(exception))
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
        catch (Exception exception) when (IsCallFailure(exception))
        {
            logger.LogWarning(exception, "GitHub call failed");

            return Result<T>.Error(GitHubErrors.RequestFailed);
        }
    }
}
