using System.Globalization;
using System.Security.Claims;
using Api.Extensions;
using Application.CreateSignIn;
using FastEndpoints;
using Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Api.Endpoints.Authentication;

public sealed class CreateSignInEndpoint(CreateSignIn handler) : Endpoint<CreateSignInRequest, SignInResponse>
{
    public override void Configure()
    {
        Post("auth/sign-in");
        Version(1);
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Connect GitHub with a personal access token";
            s.Description = "The token is validated against GitHub and kept in the session cookie, never in the database.";
        });
    }

    public override async Task HandleAsync(CreateSignInRequest req, CancellationToken ct)
    {
        var result = await handler.ExecuteAsync(req, ct);

        var resultFailed = await this.SendIfFailedAsync(result);
        if (resultFailed)
        {
            return;
        }

        await SignInAsync(result.Value);

        await Send.OkAsync(
            new SignInResponse
            {
                GitHubId = result.Value.GitHubId,
                Login = result.Value.Login,
                AvatarUrl = result.Value.AvatarUrl,
            },
            ct);
    }

    private Task SignInAsync(CreateSignInResponse signIn)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, signIn.GitHubId.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, signIn.Login),
        };

        if (signIn.AvatarUrl is not null)
        {
            claims.Add(new Claim(ClaimsPrincipalExtensions.AvatarUrlClaimType, signIn.AvatarUrl));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var properties = new AuthenticationProperties { IsPersistent = true };

        // The token rides in the encrypted ticket, which is what GitHubUserTokenHandler reads
        // on every later call. Nothing about it is written to the database.
        properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = signIn.AccessToken }]);

        return HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            properties);
    }
}

public sealed record SignInResponse
{
    public long GitHubId { get; init; }

    public string Login { get; init; } = null!;

    public string? AvatarUrl { get; init; }
}
