using System.Net;
using System.Net.Http.Headers;
using Infrastructure.Services.GitHub;
using Polly;
using Polly.Retry;
using Xunit;

namespace UnitTests.Services;

public class GitHubRetryPolicyTests
{
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.BadGateway, true)]
    [InlineData(HttpStatusCode.RequestTimeout, true)]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.NotFound, false)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.OK, false)]
    public async Task ShouldHandleAsync_ForAStatusCode_RetriesOnlyWhatCanSucceedLater(HttpStatusCode status, bool expected)
    {
        // Arrange
        using var response = new HttpResponseMessage(status);

        // Act
        var shouldRetry = await GitHubRetryPolicy.ShouldHandleAsync(RetryArguments(response));

        // Assert
        Assert.Equal(expected, shouldRetry);
    }

    [Fact]
    public async Task ShouldHandleAsync_ForA403WithNoQuotaLeft_TreatsItAsTheRateLimit()
    {
        // Arrange: GitHub reports its secondary rate limit as 403, not 429.
        using var rateLimited = new HttpResponseMessage(HttpStatusCode.Forbidden);
        rateLimited.Headers.Add("x-ratelimit-remaining", "0");

        using var forbidden = new HttpResponseMessage(HttpStatusCode.Forbidden);
        forbidden.Headers.Add("x-ratelimit-remaining", "4999");

        // Act
        var retriesRateLimited = await GitHubRetryPolicy.ShouldHandleAsync(RetryArguments(rateLimited));
        var retriesForbidden = await GitHubRetryPolicy.ShouldHandleAsync(RetryArguments(forbidden));

        // Assert
        Assert.True(retriesRateLimited);
        Assert.False(retriesForbidden);
    }

    [Fact]
    public async Task DelayAsync_ForRetryAfter_HonoursShortWaitsAndIgnoresLongOnes()
    {
        // Arrange
        using var shortWait = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        shortWait.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(5));

        using var longWait = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        longWait.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromHours(1));

        // Act
        var honoured = await GitHubRetryPolicy.DelayAsync(DelayArguments(shortWait));
        var ignored = await GitHubRetryPolicy.DelayAsync(DelayArguments(longWait));

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(5), honoured);
        Assert.Null(ignored);
    }

    private static RetryPredicateArguments<HttpResponseMessage> RetryArguments(HttpResponseMessage response) =>
        new(ResilienceContextPool.Shared.Get(), Outcome.FromResult(response), attemptNumber: 0);

    private static RetryDelayGeneratorArguments<HttpResponseMessage> DelayArguments(HttpResponseMessage response) =>
        new(ResilienceContextPool.Shared.Get(), Outcome.FromResult(response), attemptNumber: 0);
}
