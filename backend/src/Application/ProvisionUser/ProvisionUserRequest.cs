namespace Application.ProvisionUser;

public sealed record ProvisionUserRequest
{
    public long GitHubId { get; init; }

    public string Login { get; init; } = null!;

    public string? AvatarUrl { get; init; }
}
