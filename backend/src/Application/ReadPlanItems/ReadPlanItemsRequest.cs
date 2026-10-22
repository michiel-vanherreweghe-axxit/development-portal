using Application.Common;
using FastEndpoints;
using FluentValidation;

namespace Application.ReadPlanItems;

public sealed record ReadPlanItemsRequest
{
    public const int MaxRangeDays = 400;

    public DateOnly From { get; init; }

    public DateOnly To { get; init; }

    public sealed class Validator : Validator<ReadPlanItemsRequest>
    {
        public Validator()
        {
            RuleFor(request => request.From)
                .Must(PlanDates.IsValid)
                .WithErrorCode("PlanItem.DateInvalid");

            RuleFor(request => request.To)
                .Must(PlanDates.IsValid)
                .WithErrorCode("PlanItem.DateInvalid");

            RuleFor(request => request)
                .Must(request => request.To >= request.From && request.To.DayNumber - request.From.DayNumber <= MaxRangeDays)
                .WithName(nameof(To))
                .WithErrorCode("PlanItem.RangeInvalid");
        }
    }
}
