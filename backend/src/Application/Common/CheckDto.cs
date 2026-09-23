namespace Application.Common;

public sealed record CheckDto
{
    public string Name { get; init; } = null!;

    public string Conclusion { get; init; } = null!;

    public DateTimeOffset? At { get; init; }

    public string? Url { get; init; }
}
