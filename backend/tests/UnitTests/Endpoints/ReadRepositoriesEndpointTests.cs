using System.Net;
using System.Net.Http.Json;
using Application.Common;
using Ardalis.Result;
using Infrastructure.Services.GitHub.Models;
using Xunit;

namespace UnitTests.Endpoints;

public class ReadRepositoriesEndpointTests(PortalApiFactory factory) : IClassFixture<PortalApiFactory>
{
    private const string BaseUrl = "/api/v1/repositories";

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
    public async Task Get_ForASignedInUser_ReturnsWhatGitHubShowsThem()
    {
        // Arrange
        var client = factory.CreateSignedInClient();
        await factory.SeedUserAsync();
        factory.GitHub.Repositories = () => Result<IReadOnlyList<GitHubRepository>>.Success(
            [new GitHubRepository("axxitbe/development-portal", true, DateTimeOffset.UtcNow)]);

        // Act
        var response = await client.GetFromJsonAsync<List<RepositoryDto>>(BaseUrl, PortalJson.Options, CancellationToken);

        // Assert
        var repository = Assert.Single(response!);
        Assert.Equal("axxitbe/development-portal", repository.FullName);
        Assert.True(repository.Private);
    }
}
