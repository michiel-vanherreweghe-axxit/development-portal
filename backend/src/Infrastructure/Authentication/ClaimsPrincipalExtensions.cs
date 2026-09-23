using System.Globalization;
using System.Security.Claims;

namespace Infrastructure.Authentication;

public static class ClaimsPrincipalExtensions
{
    public const string AvatarUrlClaimType = "urn:github:avatar";

    public static long? GetGitHubId(this ClaimsPrincipal? principal) =>
        long.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), CultureInfo.InvariantCulture, out var id)
            ? id
            : null;

    public static string? GetLogin(this ClaimsPrincipal? principal) => principal?.FindFirstValue(ClaimTypes.Name);

    public static string? GetAvatarUrl(this ClaimsPrincipal? principal) => principal?.FindFirstValue(AvatarUrlClaimType);
}
