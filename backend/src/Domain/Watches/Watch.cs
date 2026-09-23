using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Ardalis.Result;

namespace Domain.Watches;

public sealed partial class Watch : Entity
{
    public const int OwnerMaxLength = 100;
    public const int RepositoryMaxLength = 100;
    public const int WorkflowFileMaxLength = 200;
    public const int DisplayNameMaxLength = 200;

    private Watch()
    {
    }

    public int UserId { get; private set; }

    public string Owner { get; private set; } = null!;

    public string Repository { get; private set; } = null!;

    public string WorkflowFile { get; private set; } = null!;

    public string? DisplayName { get; private set; }

    public int SortOrder { get; private set; }

    public string FullName => $"{Owner}/{Repository}";

    public static Result<Watch> Create(
        int userId,
        string repositoryFullName,
        string workflowFile,
        string? displayName,
        int sortOrder)
    {
        var errors = new List<ValidationError>();

        if (!TryParseFullName(repositoryFullName, out var owner, out var repository))
        {
            errors.Add(new ValidationError { Identifier = nameof(Repository), ErrorCode = "Watch.RepositoryInvalid" });
        }

        if (!IsValidWorkflowFile(workflowFile))
        {
            errors.Add(new ValidationError { Identifier = nameof(WorkflowFile), ErrorCode = "Watch.WorkflowFileInvalid" });
        }

        if (displayName?.Length > DisplayNameMaxLength)
        {
            errors.Add(new ValidationError { Identifier = nameof(DisplayName), ErrorCode = "Watch.DisplayNameTooLong" });
        }

        return errors.Count > 0
            ? Result<Watch>.Invalid(errors)
            : Result<Watch>.Success(new Watch
            {
                UserId = userId,
                Owner = owner!,
                Repository = repository!,
                WorkflowFile = workflowFile,
                DisplayName = displayName,
                SortOrder = sortOrder,
            });
    }

    public static bool TryParseFullName(
        string? fullName,
        [NotNullWhen(true)] out string? owner,
        [NotNullWhen(true)] out string? repository)
    {
        owner = null;
        repository = null;

        var match = fullName is null ? null : FullNamePattern().Match(fullName);
        if (match is not { Success: true })
        {
            return false;
        }

        owner = match.Groups[1].Value;
        repository = match.Groups[2].Value;

        return true;
    }

    public static bool IsValidFullName(string? fullName) => TryParseFullName(fullName, out _, out _);

    public static bool IsValidWorkflowFile(string? workflowFile) =>
        !string.IsNullOrWhiteSpace(workflowFile) &&
        workflowFile.Length <= WorkflowFileMaxLength &&
        workflowFile.IndexOfAny(['/', '\\']) < 0;

    // Stricter than GitHub allows, so a name can never walk out of its url segment.
    [GeneratedRegex(@"^([A-Za-z0-9._-]{1,100})/([A-Za-z0-9._-]{1,100})$")]
    private static partial Regex FullNamePattern();
}
