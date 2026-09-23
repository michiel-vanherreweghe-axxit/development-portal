using System.ComponentModel.DataAnnotations;

namespace Api.Configuration;

public sealed class PortalConfiguration
{
    public const string SectionName = "Portal";

    [Required]
    public Uri FrontendUri { get; set; } = null!;

    // Parent domain shared by the frontend and this api, so the session cookie stays same-site.
    public string? CookieDomain { get; set; }

    public string Origin() => FrontendUri.GetLeftPart(UriPartial.Authority);
}
