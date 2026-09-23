using Application.Common;
using Ardalis.Result;
using Domain.Watches;
using Infrastructure.Services.GitHub;
using Infrastructure.Services.GitHub.Models;

namespace Application.MergePullRequest;

public sealed class MergePullRequest(IGitHubService gitHubService)
{
    // Green is named rather than inferred: a check that is anything else — failed, still running,
    // waiting for a human, or carrying a conclusion GitHub has not taught us — blocks the merge.
    // Mirrored in the frontend's model.ts.
    private static readonly string[] Green = ["success", "skipped", "neutral"];

    public async Task<Result> ExecuteAsync(PullRequestRequest request, CancellationToken cancellationToken = default)
    {
        var pullRequest = await gitHubService.GetPullRequestAsync(
            request.Owner,
            request.Repository,
            request.Number,
            cancellationToken);

        if (!pullRequest.IsSuccess)
        {
            return Result.Error(pullRequest.Errors.FirstOrDefault());
        }

        // Read back rather than trust the caller: the dashboard it clicked from can be a minute old.
        var checkRuns = await gitHubService.GetCheckRunsAsync(
            request.Owner,
            request.Repository,
            pullRequest.Value.Head.Sha,
            cancellationToken);

        var reviews = await gitHubService.GetReviewsAsync(
            request.Owner,
            request.Repository,
            request.Number,
            cancellationToken);

        return !checkRuns.IsSuccess || !reviews.IsSuccess
            ? Result.Error(GitHubErrors.RequestFailed)
            : !IsReadyToMerge(pullRequest.Value, checkRuns.Value, reviews.Value)
                ? Result.Conflict("PullRequest.NotReady")
                : await gitHubService.MergePullRequestAsync(
                    request.Owner,
                    request.Repository,
                    request.Number,
                    pullRequest.Value.Head.Sha,
                    cancellationToken);
    }

    // The portal's own bar: out of draft, no reviewer asking for changes, and every check
    // finished green. An unreviewed pull request may merge.
    private static bool IsReadyToMerge(
        GitHubPullRequest pullRequest,
        IReadOnlyList<GitHubCheckRun> checkRuns,
        IReadOnlyList<GitHubReview> reviews) =>
        !pullRequest.Draft &&
        GitHubMapper.ToReviewState(reviews) != ReviewState.ChangesRequested &&
        checkRuns.Count > 0 &&
        checkRuns.All(checkRun => checkRun.Status == "completed" && Green.Contains(checkRun.Conclusion));
}
