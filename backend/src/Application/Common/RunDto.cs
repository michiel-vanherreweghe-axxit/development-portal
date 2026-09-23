namespace Application.Common;

public sealed record RunDto
{
    // A run that is still going has no conclusion yet, so its status stands in.
    public string Conclusion { get; init; } = null!;

    public string Url { get; init; } = null!;

    public DateTimeOffset At { get; init; }

    public int Number { get; init; }

    public string? Branch { get; init; }
}
