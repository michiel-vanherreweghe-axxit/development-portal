using Domain.Watches;
using FastEndpoints;
using FluentValidation;

namespace Application.ReadIssues;

public sealed record ReadIssuesRequest
{
    public const int QueryMaxLength = 200;

    public string Repository { get; init; } = null!;

    public string? Query { get; init; }

    public sealed class Validator : Validator<ReadIssuesRequest>
    {
        public Validator()
        {
            RuleFor(request => request.Repository)
                .Must(Watch.IsValidFullName)
                .WithErrorCode("PlanItem.RepositoryInvalid");

            RuleFor(request => request.Query)
                .MaximumLength(QueryMaxLength)
                .WithErrorCode("PlanItem.QueryTooLong");
        }
    }
}
