using System.Net;
using System.Net.Http.Json;
using Application.Common;
using Application.UpdateWatches;
using Xunit;

namespace UnitTests.Endpoints;

public class UpdateWatchesEndpointTests(PortalApiFactory factory) : IClassFixture<PortalApiFactory>
{
    private const string BaseUrl = "/api/v1/watches";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(BaseUrl, new UpdateWatchesRequest(), CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithWatches_ReplacesTheListAndReadsBackTheSame()
    {
        // Arrange
        var client = factory.CreateSignedInClient();
        await factory.SeedUserAsync();
        var request = new UpdateWatchesRequest
        {
            RunLimit = 5,
            Watches =
            [
                new WatchDto
                {
                    Repository = "axxitbe/development-portal",
                    WorkflowFile = "release-api.yml",
                    DisplayName = "Release Api",
                },
            ],
        };

        // Act
        var response = await client.PostAsJsonAsync(BaseUrl, request, CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        var reloaded = await client.GetFromJsonAsync<WatchesResponse>(BaseUrl, PortalJson.Options, CancellationToken);
        Assert.Equal(5, reloaded!.RunLimit);
        Assert.Equal("axxitbe/development-portal", Assert.Single(reloaded.Watches).Repository);
    }

    [Theory]
    [InlineData("not-a-repository", "ci.yml", 10)]
    [InlineData("owner/repository", "../../etc/passwd", 10)]
    [InlineData("owner/repository", "ci.yml", 0)]
    public async Task Post_WithAMalformedWatch_ReturnsBadRequest(string repository, string workflowFile, int runLimit)
    {
        // Arrange
        var client = factory.CreateSignedInClient();
        await factory.SeedUserAsync();
        var request = new UpdateWatchesRequest
        {
            RunLimit = runLimit,
            Watches = [new WatchDto { Repository = repository, WorkflowFile = workflowFile }],
        };

        // Act
        var response = await client.PostAsJsonAsync(BaseUrl, request, CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
