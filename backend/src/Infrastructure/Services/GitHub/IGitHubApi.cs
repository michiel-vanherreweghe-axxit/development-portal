using Infrastructure.Services.GitHub.Models;
using Refit;

namespace Infrastructure.Services.GitHub;

internal interface IGitHubApi
{
    // Takes the token explicitly: it is called while signing in, before there is a session to read it from.
    [Get("/user")]
    public Task<GitHubAccount> GetAccountAsync([Authorize("Bearer")] string accessToken, CancellationToken cancellationToken);

    [Get("/user/repos?sort=pushed&affiliation=owner,collaborator,organization_member")]
    public Task<IReadOnlyList<GitHubRepository>> GetRepositoriesAsync(
        [AliasAs("page")] int page,
        [AliasAs("per_page")] int perPage,
        CancellationToken cancellationToken);

    [Get("/repos/{owner}/{repository}/actions/workflows")]
    public Task<GitHubWorkflowList> GetWorkflowsAsync(
        string owner,
        string repository,
        [AliasAs("page")] int page,
        [AliasAs("per_page")] int perPage,
        CancellationToken cancellationToken);

    [Get("/repos/{owner}/{repository}/actions/workflows/{workflowFile}/runs")]
    public Task<GitHubRunList> GetWorkflowRunsAsync(
        string owner,
        string repository,
        string workflowFile,
        [AliasAs("per_page")] int perPage,
        CancellationToken cancellationToken);

    [Get("/repos/{owner}/{repository}/pulls?state=open&sort=updated&direction=desc")]
    public Task<IReadOnlyList<GitHubPullRequest>> GetPullRequestsAsync(
        string owner,
        string repository,
        [AliasAs("per_page")] int perPage,
        CancellationToken cancellationToken);

    [Get("/repos/{owner}/{repository}/pulls/{number}")]
    public Task<GitHubPullRequest> GetPullRequestAsync(
        string owner,
        string repository,
        int number,
        CancellationToken cancellationToken);

    [Patch("/repos/{owner}/{repository}/pulls/{number}")]
    public Task ClosePullRequestAsync(
        string owner,
        string repository,
        int number,
        [Body] GitHubPullRequestUpdate update,
        CancellationToken cancellationToken);

    [Put("/repos/{owner}/{repository}/pulls/{number}/merge")]
    public Task MergePullRequestAsync(
        string owner,
        string repository,
        int number,
        [Body] GitHubMerge merge,
        CancellationToken cancellationToken);

    [Get("/repos/{owner}/{repository}/commits/{sha}/check-runs")]
    public Task<GitHubCheckRunList> GetCheckRunsAsync(
        string owner,
        string repository,
        string sha,
        [AliasAs("page")] int page,
        [AliasAs("per_page")] int perPage,
        CancellationToken cancellationToken);

    [Get("/repos/{owner}/{repository}/pulls/{number}/reviews")]
    public Task<IReadOnlyList<GitHubReview>> GetReviewsAsync(
        string owner,
        string repository,
        int number,
        [AliasAs("page")] int page,
        [AliasAs("per_page")] int perPage,
        CancellationToken cancellationToken);

    [Get("/repos/{owner}/{repository}/issues/{number}")]
    public Task<GitHubIssue> GetIssueAsync(
        string owner,
        string repository,
        int number,
        CancellationToken cancellationToken);

    [Get("/search/issues?sort=updated&order=desc")]
    public Task<GitHubIssueSearch> SearchIssuesAsync(
        [AliasAs("q")] string query,
        [AliasAs("per_page")] int perPage,
        CancellationToken cancellationToken);
}
