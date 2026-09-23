namespace Application.Common;

public sealed record WorkflowDto
{
    public string Name { get; init; } = null!;

    public string File { get; init; } = null!;
}
