using Application.Common;
using Domain.Planning;
using Domain.Watches;
using FastEndpoints;
using FluentValidation;

namespace Application.CreatePlanItem;

public sealed record CreatePlanItemRequest
{
    public PlanItemKind Kind { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? Repository { get; init; }

    public int? IssueNumber { get; init; }

    public PlanSpan Span { get; init; }

    public DateOnly Date { get; init; }

    public sealed class Validator : Validator<CreatePlanItemRequest>
    {
        public Validator()
        {
            RuleFor(request => request.Kind).IsInEnum().WithErrorCode("PlanItem.KindInvalid");
            RuleFor(request => request.Span).IsInEnum().WithErrorCode("PlanItem.SpanInvalid");

            RuleFor(request => request.Date)
                .Must(PlanDates.IsValid)
                .WithErrorCode("PlanItem.DateInvalid");

            When(request => request.Kind == PlanItemKind.Issue, () =>
            {
                RuleFor(request => request.Repository)
                    .Must(Watch.IsValidFullName)
                    .WithErrorCode("PlanItem.RepositoryInvalid");

                RuleFor(request => request.IssueNumber)
                    .Must(number => number > 0)
                    .WithErrorCode("PlanItem.IssueNumberInvalid");
            });
        }
    }
}
