using Ardalis.Result;
using Domain.Watches;

namespace Domain.Planning;

public sealed class PlanItem : Entity
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    private PlanItem()
    {
    }

    public int UserId { get; private set; }

    public string Title { get; private set; } = null!;

    public string? Description { get; private set; }

    public string? Owner { get; private set; }

    public string? Repository { get; private set; }

    public int? IssueNumber { get; private set; }

    public PlanSpan Span { get; private set; }

    public DateOnly Start { get; private set; }

    public bool IsCompleted { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsIssue => IssueNumber is not null;

    public static Result<PlanItem> CreateTodo(
        int userId,
        string? title,
        string? description,
        PlanSpan span,
        DateOnly date)
    {
        var errors = ValidateText(title, description);

        return errors.Count > 0
            ? Result<PlanItem>.Invalid(errors)
            : Result<PlanItem>.Success(new PlanItem
            {
                UserId = userId,
                Title = title!.Trim(),
                Description = NormaliseDescription(description),
                Span = span,
                Start = StartOf(span, date),
            });
    }

    public static Result<PlanItem> CreateIssue(
        int userId,
        string? repositoryFullName,
        int issueNumber,
        string? title,
        PlanSpan span,
        DateOnly date)
    {
        var errors = new List<ValidationError>();

        if (!Watch.TryParseFullName(repositoryFullName, out var owner, out var repository))
        {
            errors.Add(new ValidationError { Identifier = nameof(Repository), ErrorCode = "PlanItem.RepositoryInvalid" });
        }

        if (issueNumber <= 0)
        {
            errors.Add(new ValidationError { Identifier = nameof(IssueNumber), ErrorCode = "PlanItem.IssueNumberInvalid" });
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            errors.Add(new ValidationError { Identifier = nameof(Title), ErrorCode = "PlanItem.TitleRequired" });
        }

        return errors.Count > 0
            ? Result<PlanItem>.Invalid(errors)
            : Result<PlanItem>.Success(new PlanItem
            {
                UserId = userId,
                Title = Truncate(title!),
                Owner = owner,
                Repository = repository,
                IssueNumber = issueNumber,
                Span = span,
                Start = StartOf(span, date),
            });
    }

    public static DateOnly StartOf(PlanSpan span, DateOnly date) =>
        span == PlanSpan.Week ? MondayOf(date) : date;

    public static DateOnly MondayOf(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public void MoveTo(PlanSpan span, DateOnly date)
    {
        Span = span;
        Start = StartOf(span, date);
    }

    public void SetCompleted(bool isCompleted)
    {
        if (isCompleted == IsCompleted)
        {
            return;
        }

        IsCompleted = isCompleted;
        CompletedAt = isCompleted ? DateTimeOffset.UtcNow : null;
    }

    public Result Edit(string? title, string? description)
    {
        if (IsIssue)
        {
            return Result.Invalid(new ValidationError { Identifier = nameof(Title), ErrorCode = "PlanItem.IssueNotEditable" });
        }

        var errors = ValidateText(title, description);
        if (errors.Count > 0)
        {
            return Result.Invalid(errors);
        }

        Title = title!.Trim();
        Description = NormaliseDescription(description);

        return Result.Success();
    }

    public void RefreshTitle(string title)
    {
        if (IsIssue && !string.IsNullOrWhiteSpace(title))
        {
            Title = Truncate(title);
        }
    }

    private static List<ValidationError> ValidateText(string? title, string? description)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(title))
        {
            errors.Add(new ValidationError { Identifier = nameof(Title), ErrorCode = "PlanItem.TitleRequired" });
        }
        else if (title.Trim().Length > TitleMaxLength)
        {
            errors.Add(new ValidationError { Identifier = nameof(Title), ErrorCode = "PlanItem.TitleTooLong" });
        }

        if (description?.Length > DescriptionMaxLength)
        {
            errors.Add(new ValidationError { Identifier = nameof(Description), ErrorCode = "PlanItem.DescriptionTooLong" });
        }

        return errors;
    }

    // The title is GitHub's, which allows more than a to-do does; a snapshot is cut, not refused.
    private static string Truncate(string title)
    {
        var trimmed = title.Trim();

        return trimmed.Length <= TitleMaxLength ? trimmed : trimmed[..TitleMaxLength];
    }

    private static string? NormaliseDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description;
}
