using Api.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Api.Authentication;

public static class AuthenticationExtensions
{
    public const string CorsPolicy = "portal-frontend";

    public static IServiceCollection AddPortalAuthentication(
        this IServiceCollection services,
        WebApplicationBuilder builder)
    {
        services
            .AddOptions<PortalConfiguration>()
            .Bind(builder.Configuration.GetSection(PortalConfiguration.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var portal = builder.Configuration.GetSection(PortalConfiguration.SectionName).Get<PortalConfiguration>()
                     ?? throw new InvalidOperationException($"The {PortalConfiguration.SectionName} section is required.");

        services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
            .WithOrigins(portal.Origin())
            .AllowCredentials()
            .AllowAnyHeader()
            .AllowAnyMethod()));

        // The session is the only scheme: the user hands us a personal access token, and the
        // sign-in endpoint puts it in this cookie. Nothing about it reaches the database.
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "development-portal.session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.Cookie.Domain = string.IsNullOrWhiteSpace(portal.CookieDomain) ? null : portal.CookieDomain;
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.SlidingExpiration = true;

                // This is an api: answer the caller instead of redirecting a browser to a login page.
                options.Events.OnRedirectToLogin = context => SetStatus(context.Response, StatusCodes.Status401Unauthorized);
                options.Events.OnRedirectToAccessDenied = context => SetStatus(context.Response, StatusCodes.Status403Forbidden);
            });

        services.AddAuthorization();

        return services;
    }

    private static Task SetStatus(HttpResponse response, int statusCode)
    {
        response.StatusCode = statusCode;

        return Task.CompletedTask;
    }
}
