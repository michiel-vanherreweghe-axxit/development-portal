using Application.Common;
using Domain.Users;
using Domain.Watches;
using FastEndpoints;
using FluentValidation;

namespace Application.UpdateWatches;

public sealed record UpdateWatchesRequest
{
    public int RunLimit { get; init; } = User.DefaultRunLimit;

    public List<WatchDto> Watches { get; init; } = [];

    public sealed class Validator : Validator<UpdateWatchesRequest>
    {
        public Validator()
        {
            RuleFor(request => request.RunLimit)
                .InclusiveBetween(User.MinimumRunLimit, User.MaximumRunLimit)
                .WithErrorCode("User.RunLimitOutOfRange");

            RuleForEach(request => request.Watches).ChildRules(watch =>
            {
                watch.RuleFor(dto => dto.Repository)
                    .Must(Watch.IsValidFullName)
                    .WithErrorCode("Watch.RepositoryInvalid");

                watch.RuleFor(dto => dto.WorkflowFile)
                    .Must(Watch.IsValidWorkflowFile)
                    .WithErrorCode("Watch.WorkflowFileInvalid");

                watch.RuleFor(dto => dto.DisplayName)
                    .MaximumLength(Watch.DisplayNameMaxLength)
                    .WithErrorCode("Watch.DisplayNameTooLong");
            });
        }
    }
}
