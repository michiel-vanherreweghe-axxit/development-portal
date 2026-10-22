using Domain.Planning;

namespace Application.Common;

public sealed record PlanItemDto
{
    public int Id { get; init; }

    public PlanItemKind Kind { get; init; }

    public string Title { get; init; } = null!;

    public string? Description { get; init; }

    public string? Repository { get; init; }

    public int? IssueNumber { get; init; }

    // Null for a to-do, and for an issue entry whose state was not read in this response.
    public IssueState? IssueState { get; init; }

    public string? IssueUrl { get; init; }

    public PlanSpan Span { get; init; }

    public DateOnly Date { get; init; }

    public bool IsCompleted { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public static PlanItemDto From(PlanItem item, IssueState? issueState = null, string? issueUrl = null) =>
        new()
        {
            Id = item.Id,
            Kind = item.IsIssue ? PlanItemKind.Issue : PlanItemKind.Todo,
            Title = item.Title,
            Description = item.Description,
            Repository = item.IsIssue ? $"{item.Owner}/{item.Repository}" : null,
            IssueNumber = item.IssueNumber,
            IssueState = item.IsIssue ? issueState : null,
            IssueUrl = item.IsIssue
                ? issueUrl ?? $"https://github.com/{item.Owner}/{item.Repository}/issues/{item.IssueNumber}"
                : null,
            Span = item.Span,
            Date = item.Start,
            IsCompleted = item.IsCompleted,
            CompletedAt = item.CompletedAt,
        };
}
