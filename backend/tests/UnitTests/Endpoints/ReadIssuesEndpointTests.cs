using System.Net;
using System.Net.Http.Json;
using Application.Common;
using Ardalis.Result;
using Infrastructure.Services.GitHub.Models;
using Xunit;

namespace UnitTests.Endpoints;

public class ReadIssuesEndpointTests(PortalApiFactory factory) : IClassFixture<PortalApiFactory>
{
    private const string BaseUrl = "/api/v1/issues";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync($"{BaseUrl}?repository=o/r", CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_ForARepository_ReturnsTheMatchingIssues()
    {
        // Arrange
        var (client, _) = await factory.CreateUserAsync();
        string? searched = null;
        factory.GitHub.IssueSearch = (owner, repository, query) =>
        {
            searched = $"{owner}/{repository}:{query}";

            return Result<IReadOnlyList<GitHubIssue>>.Success(
                [new GitHubIssue(3, "Crash on start", "open", "https://gh/o/r/issues/3", "https://api.gh/repos/o/r", null)]);
        };

        // Act
        var issues = await client.GetFromJsonAsync<List<IssueDto>>(
            $"{BaseUrl}?repository=axxitbe/portal&query=%20crash%20",
            PortalJson.Options,
            CancellationToken);

        // Assert
        Assert.Equal("axxitbe/portal:crash", searched);
        var issue = Assert.Single(issues!);
        Assert.Equal(3, issue.Number);
        Assert.Equal("https://gh/o/r/issues/3", issue.Url);
    }

    [Theory]
    [InlineData("no-slash")]
    [InlineData("o/r/../x")]
    public async Task Get_WithAnInvalidRepository_ReturnsBadRequest(string repository)
    {
        // Arrange
        var (client, _) = await factory.CreateUserAsync();

        // Act
        var response = await client.GetAsync($"{BaseUrl}?repository={Uri.EscapeDataString(repository)}", CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_WhenGitHubFails_ReturnsAServerError()
    {
        // Arrange
        var (client, _) = await factory.CreateUserAsync();
        factory.GitHub.IssueSearch = (_, _, _) => FakeGitHubService.Unavailable<IReadOnlyList<GitHubIssue>>();

        // Act
        var response = await client.GetAsync($"{BaseUrl}?repository=o/r", CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
