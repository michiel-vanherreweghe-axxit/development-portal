using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Api.Endpoints.Authentication;

public sealed class DeleteSignInEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("auth/sign-in");
        Version(1);
        Summary(s => s.Summary = "Sign the current user out");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        await Send.NoContentAsync(ct);
    }
}
