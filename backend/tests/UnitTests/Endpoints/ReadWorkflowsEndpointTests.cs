using System.Net;
using System.Net.Http.Json;
using Application.Common;
using Ardalis.Result;
using Infrastructure.Services.GitHub.Models;
using Xunit;

namespace UnitTests.Endpoints;

public class ReadWorkflowsEndpointTests(PortalApiFactory factory) : IClassFixture<PortalApiFactory>
{
    private const string BaseUrl = "/api/v1/workflows";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync($"{BaseUrl}?repository=axxitbe/development-portal", CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_ForARepository_ReturnsItsWorkflowFiles()
    {
        // Arrange
        var client = factory.CreateSignedInClient();
        await factory.SeedUserAsync();
        factory.GitHub.Workflows = (_, _) => Result<IReadOnlyList<GitHubWorkflow>>.Success(
            [new GitHubWorkflow("Release Api", ".github/workflows/release-api.yml")]);

        // Act
        var response = await client.GetFromJsonAsync<List<WorkflowDto>>($"{BaseUrl}?repository=axxitbe/development-portal", PortalJson.Options, CancellationToken);

        // Assert
        var workflow = Assert.Single(response!);
        Assert.Equal("release-api.yml", workflow.File);
    }

    [Fact]
    public async Task Get_WithAMalformedRepository_ReturnsBadRequest()
    {
        // Arrange
        var client = factory.CreateSignedInClient();
        await factory.SeedUserAsync();

        // Act
        var response = await client.GetAsync($"{BaseUrl}?repository=../../secrets", CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
