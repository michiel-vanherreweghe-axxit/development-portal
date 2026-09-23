using System.Net;
using System.Net.Http.Json;
using Ardalis.Result;
using Application.Common;
using Infrastructure.Services.GitHub;
using Infrastructure.Services.GitHub.Models;
using Xunit;

namespace UnitTests.Endpoints;

public class ReadRunsEndpointTests(PortalApiFactory factory) : IClassFixture<PortalApiFactory>
{
    private const string BaseUrl = "/api/v1/runs";

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
    public async Task Get_WithOneUnreadableRepository_MarksThatRowAndKeepsTheRest()
    {
        // Arrange
        var client = factory.CreateSignedInClient();
        await factory.SeedUserAsync(
            ("axxitbe/development-portal", "release-api.yml"),
            ("axxitbe/development-portal", "gone.yml"));

        factory.GitHub.Runs = (_, _, workflowFile) => workflowFile == "gone.yml"
            ? FakeGitHubService.Unavailable<IReadOnlyList<GitHubRun>>()
            : Result<IReadOnlyList<GitHubRun>>.Success(
                [new GitHubRun(null, "in_progress", "https://gh/1", DateTimeOffset.UtcNow, 7, "main")]);

        // Act
        var response = await client.GetFromJsonAsync<List<WatchRunsDto>>(BaseUrl, PortalJson.Options, CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(2, response.Count);

        var healthy = response.Single(row => row.WorkflowFile == "release-api.yml");
        Assert.Null(healthy.Error);
        Assert.Equal("in_progress", Assert.Single(healthy.Runs).Conclusion);

        var broken = response.Single(row => row.WorkflowFile == "gone.yml");
        Assert.Equal(GitHubErrors.RequestFailed, broken.Error);
        Assert.Empty(broken.Runs);
    }
}
