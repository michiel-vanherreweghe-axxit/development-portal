using System.Net;
using System.Net.Http.Json;
using Application.Common;
using Domain.Users;
using Xunit;

namespace UnitTests.Endpoints;

public class ReadWatchesEndpointTests(PortalApiFactory factory) : IClassFixture<PortalApiFactory>
{
    private const string BaseUrl = "/api/v1/watches";

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
    public async Task Get_ForASignedInUser_ReturnsTheirWatches()
    {
        // Arrange
        var client = factory.CreateSignedInClient();
        await factory.SeedUserAsync(("axxitbe/development-portal", "release-api.yml"));

        // Act
        var response = await client.GetFromJsonAsync<WatchesResponse>(BaseUrl, PortalJson.Options, CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(User.DefaultRunLimit, response.RunLimit);
        var watch = Assert.Single(response.Watches);
        Assert.Equal("axxitbe/development-portal", watch.Repository);
        Assert.Equal("release-api.yml", watch.WorkflowFile);
    }
}
