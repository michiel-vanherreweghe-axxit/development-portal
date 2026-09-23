using Domain.Watches;

namespace Application.Common;

public sealed record PullRequestDto
{
    public string Repository { get; init; } = null!;

    public int Number { get; init; }

    public string Title { get; init; } = null!;

    public string? Author { get; init; }

    public string Head { get; init; } = null!;

    public string Base { get; init; } = null!;

    public bool Draft { get; init; }

    public DateTimeOffset At { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public string Url { get; init; } = null!;

    public IReadOnlyList<CheckDto> Checks { get; init; } = [];

    public ReviewState ReviewState { get; init; }

    public string? Error { get; init; }
}
