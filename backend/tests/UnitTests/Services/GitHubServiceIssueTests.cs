using System.Net;
using System.Text;
using System.Text.Json;
using Infrastructure.Services.GitHub;
using Microsoft.Extensions.Logging.Abstractions;
using Refit;
using Xunit;

namespace UnitTests.Services;

public class GitHubServiceIssueTests
{
    [Theory]
    [InlineData(HttpStatusCode.NotFound, null, true)]
    [InlineData(HttpStatusCode.Gone, null, true)]
    [InlineData(HttpStatusCode.Forbidden, null, true)]
    [InlineData(HttpStatusCode.Forbidden, "4999", true)]
    [InlineData(HttpStatusCode.Forbidden, "0", false)]
    [InlineData(HttpStatusCode.InternalServerError, null, false)]
    public async Task GetIssueAsync_WhenGitHubFails_SeparatesAGoneIssueFromAFailingCall(
        HttpStatusCode status,
        string? remaining,
        bool isUnavailable)
    {
        // Arrange
        var service = CreateService(_ => Respond(status, "{}", remaining));

        // Act
        var result = await service.GetIssueAsync("o", "r", 1, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(isUnavailable, result.Status == Ardalis.Result.ResultStatus.NotFound);
        Assert.Equal(
            isUnavailable ? GitHubErrors.Unavailable : GitHubErrors.RequestFailed,
            result.Errors.Single());
    }

    [Fact]
    public async Task GetIssueAsync_ForAnIssue_ReadsItAndMarksPullRequests()
    {
        // Arrange
        var service = CreateService(_ => Respond(
            HttpStatusCode.OK,
            """{"number":4,"title":"A pull","state":"open","html_url":"https://gh/o/r/pull/4","repository_url":"https://api.gh/repos/o/r","pull_request":{"url":"x"}}"""));

        // Act
        var result = await service.GetIssueAsync("o", "r", 4, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("A pull", result.Value.Title);
        Assert.NotNull(result.Value.PullRequest);
    }

    [Fact]
    public async Task SearchIssuesAsync_ScopesTheQueryAndDropsOtherRepositoriesAndPullRequests()
    {
        // Arrange
        string? requested = null;
        var service = CreateService(request =>
        {
            requested = request.RequestUri!.Query;

            return Respond(HttpStatusCode.OK, JsonSerializer.Serialize(new
            {
                items = new[]
                {
                    new { number = 1, title = "Mine", state = "open", html_url = "u1", repository_url = "https://api.gh/repos/O/R" },
                    new { number = 2, title = "Elsewhere", state = "open", html_url = "u2", repository_url = "https://api.gh/repos/x/y" },
                },
            }));
        });

        // Act
        var result = await service.SearchIssuesAsync("o", "r", "repo:x/y crash", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([1], result.Value.Select(issue => issue.Number));
        Assert.Contains("per_page=30", requested);
        Assert.StartsWith("?sort=updated&order=desc&q=repo%3Ao%2Fr", requested);
    }

    [Fact]
    public async Task SearchIssuesAsync_WhenGitHubFails_ReturnsAnError()
    {
        // Arrange
        var service = CreateService(_ => Respond(HttpStatusCode.Forbidden, "{}", "0"));

        // Act
        var result = await service.SearchIssuesAsync("o", "r", "crash", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(GitHubErrors.RequestFailed, result.Errors.Single());
    }

    private static HttpResponseMessage Respond(HttpStatusCode status, string json, string? remaining = null)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        if (remaining is not null)
        {
            response.Headers.Add("x-ratelimit-remaining", remaining);
        }

        return response;
    }

    private static GitHubService CreateService(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var settings = new RefitSettings
        {
            ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            }),
        };

        var client = new HttpClient(new StubHandler(respond)) { BaseAddress = new Uri("https://api.gh") };

        return new GitHubService(RestService.For<IGitHubApi>(client, settings), NullLogger<GitHubService>.Instance);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(WithRequest(respond(request), request));

        private static HttpResponseMessage WithRequest(HttpResponseMessage response, HttpRequestMessage request)
        {
            response.RequestMessage = request;

            return response;
        }
    }
}
