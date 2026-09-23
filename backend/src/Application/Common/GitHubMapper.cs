using Domain.Watches;
using Infrastructure.Services.GitHub.Models;

namespace Application.Common;

public static class GitHubMapper
{
    public static RunDto ToDto(GitHubRun run) =>
        new()
        {
            Conclusion = run.Conclusion ?? run.Status,
            Url = run.HtmlUrl,
            At = run.CreatedAt,
            Number = run.RunNumber,
            Branch = run.HeadBranch,
        };

    public static CheckDto ToDto(GitHubCheckRun checkRun) =>
        new()
        {
            Name = checkRun.Name,
            Conclusion = checkRun.Conclusion ?? checkRun.Status,
            At = checkRun.CompletedAt ?? checkRun.StartedAt,
            Url = checkRun.HtmlUrl,
        };

    public static RepositoryDto ToDto(GitHubRepository repository) =>
        new()
        {
            FullName = repository.FullName,
            Private = repository.Private,
            PushedAt = repository.PushedAt,
        };

    public static WorkflowDto ToDto(GitHubWorkflow workflow) =>
        new()
        {
            Name = workflow.Name,
            File = workflow.Path.Split('/')[^1],
        };

    public static PullRequestDto ToDto(GitHubPullRequest pullRequest, string repository) =>
        new()
        {
            Repository = repository,
            Number = pullRequest.Number,
            Title = pullRequest.Title,
            Author = pullRequest.User?.Login,
            Head = pullRequest.Head.Ref,
            Base = pullRequest.Base.Ref,
            Draft = pullRequest.Draft,
            At = pullRequest.CreatedAt,
            UpdatedAt = pullRequest.UpdatedAt,
            Url = pullRequest.HtmlUrl,
        };

    // The latest verdict per reviewer, collapsed to one state.
    public static ReviewState ToReviewState(IReadOnlyList<GitHubReview> reviews)
    {
        var latest = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var review in reviews)
        {
            if (review.User is { Login: var login } && review.State is "APPROVED" or "CHANGES_REQUESTED" or "DISMISSED")
            {
                latest[login] = review.State;
            }
        }

        return latest.ContainsValue("CHANGES_REQUESTED")
            ? ReviewState.ChangesRequested
            : latest.ContainsValue("APPROVED")
                ? ReviewState.Approved
                : ReviewState.ReviewRequired;
    }
}
