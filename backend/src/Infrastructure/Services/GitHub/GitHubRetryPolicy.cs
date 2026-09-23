using System.Net;
using Polly.Retry;
using Polly.Timeout;

namespace Infrastructure.Services.GitHub;

internal static class GitHubRetryPolicy
{
    private static readonly TimeSpan MaxHonouredDelay = TimeSpan.FromSeconds(30);

    public static ValueTask<bool> ShouldHandleAsync(RetryPredicateArguments<HttpResponseMessage> arguments)
    {
        if (arguments.Outcome.Exception is HttpRequestException or TimeoutRejectedException)
        {
            return ValueTask.FromResult(true);
        }

        var response = arguments.Outcome.Result;
        if (response is null)
        {
            return ValueTask.FromResult(false);
        }

        // 404 is an answer, not a failure: a deleted workflow must reach its dashboard row.
        var shouldRetry = response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
                          || (int)response.StatusCode >= 500
                          || IsSecondaryRateLimit(response);

        return ValueTask.FromResult(shouldRetry);
    }

    public static ValueTask<TimeSpan?> DelayAsync(RetryDelayGeneratorArguments<HttpResponseMessage> arguments)
    {
        var delay = RetryAfter(arguments.Outcome.Result);

        // Null falls back to the pipeline's exponential backoff with jitter.
        return ValueTask.FromResult<TimeSpan?>(
            delay is { } wait && wait > TimeSpan.Zero && wait <= MaxHonouredDelay ? wait : null);
    }

    // GitHub reports its secondary rate limit as 403 with an exhausted quota, not as 429.
    private static bool IsSecondaryRateLimit(HttpResponseMessage response) =>
        response.StatusCode == HttpStatusCode.Forbidden &&
        response.Headers.TryGetValues("x-ratelimit-remaining", out var remaining) &&
        remaining.FirstOrDefault() == "0";

    private static TimeSpan? RetryAfter(HttpResponseMessage? response) =>
        response?.Headers.RetryAfter switch
        {
            { Delta: { } delta } => delta,
            { Date: { } date } => date - DateTimeOffset.UtcNow,
            _ => null,
        };
}
