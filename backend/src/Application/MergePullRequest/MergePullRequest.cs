using Application.Common;
using Ardalis.Result;
using Domain.Watches;
using Infrastructure.Services.GitHub;
using Infrastructure.Services.GitHub.Models;

namespace Application.MergePullRequest;

public sealed class MergePullRequest(IGitHubService gitHubService)
{
    // The conclusions the dashboard colours red and amber; mirrored in the frontend's model.ts.
    private static readonly string[] Failing = ["failure", "timed_out", "startup_failure"];
    private static readonly string[] Running = ["in_progress", "queued", "requested", "waiting"];

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

    // The portal's own bar, mirrored in the frontend's model.ts: out of draft, no reviewer asking
    // for changes, and every check it has finished green. An unreviewed pull request may merge.
    private static bool IsReadyToMerge(
        GitHubPullRequest pullRequest,
        IReadOnlyList<GitHubCheckRun> checkRuns,
        IReadOnlyList<GitHubReview> reviews) =>
        !pullRequest.Draft &&
        GitHubMapper.ToReviewState(reviews) != ReviewState.ChangesRequested &&
        checkRuns.Count > 0 &&
        !checkRuns.Any(checkRun => Failing.Contains(checkRun.Conclusion ?? checkRun.Status)) &&
        !checkRuns.Any(checkRun => Running.Contains(checkRun.Conclusion ?? checkRun.Status));
}
