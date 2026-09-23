using System.Net;
using Ardalis.Result;
using Infrastructure.Services.GitHub.Models;
using Xunit;

namespace UnitTests.Endpoints;

public class PullRequestActionsEndpointTests(PortalApiFactory factory) : IClassFixture<PortalApiFactory>
{
    private const string Repository = "axxitbe/development-portal";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync($"/api/v1/pull-requests/{Repository}/12/close", null, CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_Close_ClosesThePullRequest()
    {
        // Arrange
        var client = factory.CreateSignedInClient();
        await factory.SeedUserAsync();

        // Act
        var response = await client.PostAsync($"/api/v1/pull-requests/{Repository}/12/close", null, CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains($"{Repository}#12", factory.GitHub.Closed);
    }

    [Fact]
    public async Task Post_Merge_WhenApprovedAndGreen_MergesTheHeadItChecked()
    {
        // Arrange
        var client = factory.CreateSignedInClient();
        await factory.SeedUserAsync();
        Approved();
        Checks("success");

        // Act
        var response = await client.PostAsync($"/api/v1/pull-requests/{Repository}/13/merge", null, CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains($"{Repository}#13@abc123", factory.GitHub.Merged);
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("action_required")]
    [InlineData("cancelled")]
    public async Task Post_Merge_WithChecksThatAreNotGreen_ReturnsConflictAndMergesNothing(string conclusion)
    {
        // Arrange
        var client = factory.CreateSignedInClient();
        await factory.SeedUserAsync();
        Approved();
        Checks(conclusion);

        // Act
        var response = await client.PostAsync($"/api/v1/pull-requests/{Repository}/14/merge", null, CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.DoesNotContain(factory.GitHub.Merged, merged => merged.Contains("#14"));
    }

    [Fact]
    public async Task Post_Merge_WithoutAnyReview_StillMerges()
    {
        // Arrange
        var client = factory.CreateSignedInClient();
        await factory.SeedUserAsync();
        factory.GitHub.Reviews = (_, _, _) => Result<IReadOnlyList<GitHubReview>>.Success([]);
        Checks("success");

        // Act
        var response = await client.PostAsync($"/api/v1/pull-requests/{Repository}/15/merge", null, CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains($"{Repository}#15@abc123", factory.GitHub.Merged);
    }

    [Fact]
    public async Task Post_Merge_WithChangesRequested_ReturnsConflict()
    {
        // Arrange
        var client = factory.CreateSignedInClient();
        await factory.SeedUserAsync();
        factory.GitHub.Reviews = (_, _, _) => Result<IReadOnlyList<GitHubReview>>.Success(
            [new GitHubReview("CHANGES_REQUESTED", new GitHubAccount(2, "reviewer", null))]);
        Checks("success");

        // Act
        var response = await client.PostAsync($"/api/v1/pull-requests/{Repository}/16/merge", null, CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.DoesNotContain(factory.GitHub.Merged, merged => merged.Contains("#16"));
    }

    [Fact]
    public async Task Post_Merge_WithACheckThatHasNotFinished_ReturnsConflict()
    {
        // Arrange
        var client = factory.CreateSignedInClient();
        await factory.SeedUserAsync();
        Approved();
        factory.GitHub.CheckRuns = (_, _, _) => Result<IReadOnlyList<GitHubCheckRun>>.Success(
            [new GitHubCheckRun("build", null, "queued", DateTimeOffset.UtcNow, null, null)]);

        // Act
        var response = await client.PostAsync($"/api/v1/pull-requests/{Repository}/17/merge", null, CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.DoesNotContain(factory.GitHub.Merged, merged => merged.Contains("#17"));
    }

    [Fact]
    public async Task Post_WithAMalformedRepository_ReturnsBadRequest()
    {
        // Arrange
        var client = factory.CreateSignedInClient();
        await factory.SeedUserAsync();

        // Act
        var response = await client.PostAsync("/api/v1/pull-requests/owner/repo$/12/close", null, CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private void Approved() =>
        factory.GitHub.Reviews = (_, _, _) => Result<IReadOnlyList<GitHubReview>>.Success(
            [new GitHubReview("APPROVED", new GitHubAccount(2, "reviewer", null))]);

    private void Checks(string conclusion) =>
        factory.GitHub.CheckRuns = (_, _, _) => Result<IReadOnlyList<GitHubCheckRun>>.Success(
            [new GitHubCheckRun("build", conclusion, "completed", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null)]);
}
