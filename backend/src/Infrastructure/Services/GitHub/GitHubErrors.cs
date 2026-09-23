namespace Infrastructure.Services.GitHub;

public static class GitHubErrors
{
    // The i18n key a dashboard row shows when its repository could not be read.
    public const string RequestFailed = "GitHub.RequestFailed";

    // GitHub would not accept the token itself, as opposed to failing to answer at all.
    public const string TokenRejected = "GitHub.TokenRejected";

    // GitHub itself said no to a write: a merge conflict, a protection rule, a moved head.
    public const string Refused = "GitHub.Refused";
}
