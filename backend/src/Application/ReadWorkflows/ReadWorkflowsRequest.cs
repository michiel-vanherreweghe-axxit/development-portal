using Domain.Watches;
using FastEndpoints;
using FluentValidation;

namespace Application.ReadWorkflows;

public sealed record ReadWorkflowsRequest
{
    public string Repository { get; init; } = null!;

    public sealed class Validator : Validator<ReadWorkflowsRequest>
    {
        public Validator() =>
            RuleFor(request => request.Repository)
                .Must(Watch.IsValidFullName)
                .WithErrorCode("Watch.RepositoryInvalid");
    }
}
