namespace Application.Common;

public sealed record WatchRunsDto
{
    public string Repository { get; init; } = null!;

    public string WorkflowFile { get; init; } = null!;

    public string? DisplayName { get; init; }

    public IReadOnlyList<RunDto> Runs { get; init; } = [];

    // One unreadable repository leaves its own row marked, instead of failing the whole dashboard.
    public string? Error { get; init; }
}
