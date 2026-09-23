using System.Net;
using System.Net.Http.Json;
using Application.CreateSignIn;
using Ardalis.Result;
using Domain.Users;
using Infrastructure.Database;
using Infrastructure.Services.GitHub;
using Infrastructure.Services.GitHub.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace UnitTests.Endpoints;

public class CreateSignInEndpointTests(PortalApiFactory factory) : IClassFixture<PortalApiFactory>
{
    private const string BaseUrl = "/api/v1/auth/sign-in";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_WithAWorkingToken_OpensASessionAndProvisionsTheUser()
    {
        // Arrange
        var client = factory.CreateClient();
        factory.GitHub.Account = _ => Result<GitHubAccount>.Success(new GitHubAccount(99, "connected-user", "https://avatars/99"));

        // Act
        var response = await client.PostAsJsonAsync(
            BaseUrl,
            new CreateSignInRequest { PersonalAccessToken = "github_pat_example" },
            CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Contains(
            response.Headers.GetValues("Set-Cookie"),
            cookie => cookie.StartsWith("development-portal.session", StringComparison.Ordinal));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await context.Users.FirstOrDefaultAsync(user => user.GitHubId == 99, CancellationToken);
        Assert.NotNull(user);
        Assert.Equal("connected-user", user.Login);
        Assert.Equal(User.DefaultRunLimit, user.RunLimit);
    }

    [Fact]
    public async Task Post_WithATokenGitHubRefuses_ReturnsBadRequestAndNoSession()
    {
        // Arrange
        var client = factory.CreateClient();
        factory.GitHub.Account = _ => Result<GitHubAccount>.Unauthorized(GitHubErrors.TokenRejected);

        // Act
        var response = await client.PostAsJsonAsync(
            BaseUrl,
            new CreateSignInRequest { PersonalAccessToken = "not-a-token" },
            CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Post_WhenGitHubCannotBeAnswered_DoesNotBlameTheToken()
    {
        // Arrange
        var client = factory.CreateClient();
        factory.GitHub.Account = _ => Result<GitHubAccount>.Error(GitHubErrors.RequestFailed);

        // Act
        var response = await client.PostAsJsonAsync(
            BaseUrl,
            new CreateSignInRequest { PersonalAccessToken = "ghp_a_perfectly_good_token" },
            CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Post_WithoutAToken_ReturnsBadRequest()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            BaseUrl,
            new CreateSignInRequest { PersonalAccessToken = "" },
            CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_NeverEchoesTheTokenBack()
    {
        // Arrange
        var client = factory.CreateClient();
        const string token = "github_pat_secret_value";
        factory.GitHub.Account = _ => Result<GitHubAccount>.Success(new GitHubAccount(99, "connected-user", null));

        // Act
        var response = await client.PostAsJsonAsync(
            BaseUrl,
            new CreateSignInRequest { PersonalAccessToken = token },
            CancellationToken);

        // Assert
        var body = await response.Content.ReadAsStringAsync(CancellationToken);
        Assert.DoesNotContain(token, body, StringComparison.Ordinal);
    }
}
