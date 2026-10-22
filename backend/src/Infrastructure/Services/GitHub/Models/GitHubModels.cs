namespace Infrastructure.Services.GitHub.Models;

// GitHub speaks snake_case; the naming policy on the Refit serializer maps these.
public sealed record GitHubRepository(string FullName, bool Private, DateTimeOffset? PushedAt);

public sealed record GitHubWorkflowList(IReadOnlyList<GitHubWorkflow> Workflows);

public sealed record GitHubWorkflow(string Name, string Path);

public sealed record GitHubRunList(IReadOnlyList<GitHubRun> WorkflowRuns);

public sealed record GitHubRun(
    string? Conclusion,
    string Status,
    string HtmlUrl,
    DateTimeOffset CreatedAt,
    int RunNumber,
    string? HeadBranch);

public sealed record GitHubPullRequest(
    int Number,
    string Title,
    GitHubAccount? User,
    GitHubReference Head,
    GitHubReference Base,
    bool Draft,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string HtmlUrl);

public sealed record GitHubReference(string Ref, string Sha);

public sealed record GitHubAccount(long Id, string Login, string? AvatarUrl);

public sealed record GitHubCheckRunList(IReadOnlyList<GitHubCheckRun> CheckRuns);

public sealed record GitHubCheckRun(
    string Name,
    string? Conclusion,
    string Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? HtmlUrl);

public sealed record GitHubReview(string State, GitHubAccount? User);

public sealed record GitHubPullRequestUpdate(string State);

// The head sha the merge was decided on, so GitHub refuses a head that moved since.
public sealed record GitHubMerge(string Sha, string MergeMethod);

// PullRequest is only present when the "issue" is really a pull request.
public sealed record GitHubIssue(
    int Number,
    string Title,
    string State,
    string HtmlUrl,
    string RepositoryUrl,
    GitHubPullRequestMarker? PullRequest);

public sealed record GitHubPullRequestMarker(string? Url);

public sealed record GitHubIssueSearch(IReadOnlyList<GitHubIssue> Items);
