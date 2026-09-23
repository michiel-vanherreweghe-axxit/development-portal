using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Services.GitHub.Configuration;

public sealed class GitHubConfiguration
{
    public const string SectionName = "GitHub";

    [Required]
    public Uri Uri { get; set; } = new("https://api.github.com/");

    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
