namespace Application.Common;

public sealed record WatchDto
{
    public string Repository { get; init; } = null!;

    public string WorkflowFile { get; init; } = null!;

    public string? DisplayName { get; init; }
}
