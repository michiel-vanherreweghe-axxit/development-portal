namespace Application.Common;

public sealed record IssueDto
{
    public int Number { get; init; }

    public string Title { get; init; } = null!;

    public string State { get; init; } = null!;

    public string Url { get; init; } = null!;
}
