namespace Application.CreateSignIn;

public sealed record CreateSignInResponse
{
    public long GitHubId { get; init; }

    public string Login { get; init; } = null!;

    public string? AvatarUrl { get; init; }

    // Handed to the endpoint for the session ticket; never serialised to the caller.
    public string AccessToken { get; init; } = null!;
}
