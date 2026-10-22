using Application.Common;
using Domain.Planning;
using FastEndpoints;
using FluentValidation;

namespace Application.UpdatePlanItem;

public sealed record UpdatePlanItemRequest
{
    public int Id { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public PlanSpan Span { get; init; }

    public DateOnly Date { get; init; }

    public bool IsCompleted { get; init; }

    public sealed class Validator : Validator<UpdatePlanItemRequest>
    {
        public Validator()
        {
            RuleFor(request => request.Span).IsInEnum().WithErrorCode("PlanItem.SpanInvalid");

            RuleFor(request => request.Date)
                .Must(PlanDates.IsValid)
                .WithErrorCode("PlanItem.DateInvalid");
        }
    }
}
