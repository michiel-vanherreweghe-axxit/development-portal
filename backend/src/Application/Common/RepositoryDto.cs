namespace Application.Common;

public sealed record RepositoryDto
{
    public string FullName { get; init; } = null!;

    public bool Private { get; init; }

    public DateTimeOffset? PushedAt { get; init; }
}
