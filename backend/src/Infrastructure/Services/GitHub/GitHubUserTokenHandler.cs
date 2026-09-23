using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Services.GitHub;

internal sealed class GitHubUserTokenHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Sign-in calls carry their own token, since there is no session to read one from yet.
        if (request.Headers.Authorization is not null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var context = httpContextAccessor.HttpContext
                      ?? throw new InvalidOperationException("GitHub is called as the signed-in user, which needs a request context.");

        var token = await context.GetTokenAsync("access_token")
                    ?? throw new InvalidOperationException("The signed-in user has no GitHub access token.");

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
