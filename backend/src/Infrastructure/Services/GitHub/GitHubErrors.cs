namespace Infrastructure.Services.GitHub;

public static class GitHubErrors
{
    // The i18n key a dashboard row shows when its repository could not be read.
    public const string RequestFailed = "GitHub.RequestFailed";

    // GitHub itself said no to a write: a merge conflict, a protection rule, a moved head.
    public const string Refused = "GitHub.Refused";
}
