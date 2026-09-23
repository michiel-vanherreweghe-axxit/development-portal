using System.Net;
using System.Net.Http.Json;
using Application.Common;
using Ardalis.Result;
using Domain.Watches;
using Infrastructure.Services.GitHub;
using Infrastructure.Services.GitHub.Models;
using Xunit;

namespace UnitTests.Endpoints;

public class ReadPullRequestsEndpointTests(PortalApiFactory factory) : IClassFixture<PortalApiFactory>
{
    private const string BaseUrl = "/api/v1/pull-requests";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(BaseUrl, CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_ForAnOpenPullRequest_CarriesItsChecksAndReviewVerdict()
    {
        // Arrange
        var client = factory.CreateSignedInClient();
        await factory.SeedUserAsync(("axxitbe/development-portal", "release-api.yml"));

        factory.GitHub.PullRequests = (owner, repository) => Result<IReadOnlyList<GitHubPullRequest>>.Success(
        [
            new GitHubPullRequest(
                12,
                "Add the thing",
                new GitHubAccount(1, "tester", null),
                new GitHubReference("feature", "abc123"),
                new GitHubReference("main", "def456"),
                false,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                $"https://gh/{owner}/{repository}/pull/12"),
        ]);

        factory.GitHub.CheckRuns = (_, _, _) => Result<IReadOnlyList<GitHubCheckRun>>.Success(
            [new GitHubCheckRun("build", "success", "completed", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "https://gh/check")]);

        factory.GitHub.Reviews = (_, _, _) => Result<IReadOnlyList<GitHubReview>>.Success(
            [new GitHubReview("APPROVED", new GitHubAccount(2, "reviewer", null))]);

        // Act
        var response = await client.GetFromJsonAsync<List<PullRequestDto>>(BaseUrl, PortalJson.Options, CancellationToken);

        // Assert
        var pullRequest = Assert.Single(response!);
        Assert.Equal(12, pullRequest.Number);
        Assert.Equal(ReviewState.Approved, pullRequest.ReviewState);
        Assert.Equal("success", Assert.Single(pullRequest.Checks).Conclusion);
        Assert.Null(pullRequest.Error);
    }

    [Fact]
    public async Task Get_WithAnUnreadableRepository_ReturnsAnErrorRowRatherThanFailing()
    {
        // Arrange
        var client = factory.CreateSignedInClient();
        await factory.SeedUserAsync(("axxitbe/development-portal", "release-api.yml"));
        factory.GitHub.PullRequests = (_, _) => FakeGitHubService.Unavailable<IReadOnlyList<GitHubPullRequest>>();

        // Act
        var response = await client.GetFromJsonAsync<List<PullRequestDto>>(BaseUrl, PortalJson.Options, CancellationToken);

        // Assert
        var row = Assert.Single(response!);
        Assert.Equal(GitHubErrors.RequestFailed, row.Error);
    }
}
