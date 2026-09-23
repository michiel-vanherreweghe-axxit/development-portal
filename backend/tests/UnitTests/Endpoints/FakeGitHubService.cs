using Ardalis.Result;
using Infrastructure.Services.GitHub;
using Infrastructure.Services.GitHub.Models;

namespace UnitTests.Endpoints;

public sealed class FakeGitHubService : IGitHubService
{
    public Func<Result<IReadOnlyList<GitHubRepository>>> Repositories { get; set; } =
        () => Result<IReadOnlyList<GitHubRepository>>.Success([]);

    public Func<string, string, Result<IReadOnlyList<GitHubWorkflow>>> Workflows { get; set; } =
        (_, _) => Result<IReadOnlyList<GitHubWorkflow>>.Success([]);

    public Func<string, string, string, Result<IReadOnlyList<GitHubRun>>> Runs { get; set; } =
        (_, _, _) => Result<IReadOnlyList<GitHubRun>>.Success([]);

    public Func<string, string, Result<IReadOnlyList<GitHubPullRequest>>> PullRequests { get; set; } =
        (_, _) => Result<IReadOnlyList<GitHubPullRequest>>.Success([]);

    public Func<string, string, int, Result<GitHubPullRequest>> PullRequest { get; set; } =
        (owner, repository, number) => Result<GitHubPullRequest>.Success(new GitHubPullRequest(
            number,
            "Add the thing",
            new GitHubAccount(1, PortalApiFactory.Login, null),
            new GitHubReference("feature", "abc123"),
            new GitHubReference("main", "def456"),
            false,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            $"https://gh/{owner}/{repository}/pull/{number}"));

    public Func<Result> Close { get; set; } = Result.Success;

    public Func<Result> Merge { get; set; } = Result.Success;

    /// <summary>What was actually written, so a test can assert the call did not happen.</summary>
    public List<string> Closed { get; } = [];

    public List<string> Merged { get; } = [];

    public Func<string, string, string, Result<IReadOnlyList<GitHubCheckRun>>> CheckRuns { get; set; } =
        (_, _, _) => Result<IReadOnlyList<GitHubCheckRun>>.Success([]);

    public Func<string, string, int, Result<IReadOnlyList<GitHubReview>>> Reviews { get; set; } =
        (_, _, _) => Result<IReadOnlyList<GitHubReview>>.Success([]);

    public Func<string, Result<GitHubAccount>> Account { get; set; } =
        _ => Result<GitHubAccount>.Success(new GitHubAccount(PortalApiFactory.GitHubId, PortalApiFactory.Login, null));

    public static Result<T> Unavailable<T>() => Result<T>.Error(GitHubErrors.RequestFailed);

    public Task<Result<GitHubAccount>> GetAccountAsync(string accessToken, CancellationToken cancellationToken = default) =>
        Task.FromResult(Account(accessToken));

    public Task<Result<IReadOnlyList<GitHubRepository>>> GetRepositoriesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Repositories());

    public Task<Result<IReadOnlyList<GitHubWorkflow>>> GetWorkflowsAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Workflows(owner, repository));

    public Task<Result<IReadOnlyList<GitHubRun>>> GetWorkflowRunsAsync(
        string owner,
        string repository,
        string workflowFile,
        int limit,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Runs(owner, repository, workflowFile));

    public Task<Result<IReadOnlyList<GitHubPullRequest>>> GetPullRequestsAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(PullRequests(owner, repository));

    public Task<Result<GitHubPullRequest>> GetPullRequestAsync(
        string owner,
        string repository,
        int number,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(PullRequest(owner, repository, number));

    public Task<Result> ClosePullRequestAsync(
        string owner,
        string repository,
        int number,
        CancellationToken cancellationToken = default)
    {
        Closed.Add($"{owner}/{repository}#{number}");

        return Task.FromResult(Close());
    }

    public Task<Result> MergePullRequestAsync(
        string owner,
        string repository,
        int number,
        string sha,
        CancellationToken cancellationToken = default)
    {
        Merged.Add($"{owner}/{repository}#{number}@{sha}");

        return Task.FromResult(Merge());
    }

    public Task<Result<IReadOnlyList<GitHubCheckRun>>> GetCheckRunsAsync(
        string owner,
        string repository,
        string sha,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(CheckRuns(owner, repository, sha));

    public Task<Result<IReadOnlyList<GitHubReview>>> GetReviewsAsync(
        string owner,
        string repository,
        int number,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Reviews(owner, repository, number));
}
