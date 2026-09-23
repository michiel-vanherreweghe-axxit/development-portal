using Domain.Watches;
using FastEndpoints;
using FluentValidation;

namespace Application.Common;

/// <summary>One pull request, addressed the way its route spells it.</summary>
public sealed record PullRequestRequest
{
    public string Owner { get; init; } = null!;

    public string Repository { get; init; } = null!;

    public int Number { get; init; }

    public string FullName => $"{Owner}/{Repository}";

    public sealed class Validator : Validator<PullRequestRequest>
    {
        public Validator()
        {
            RuleFor(request => request.FullName)
                .Must(Watch.IsValidFullName)
                .WithErrorCode("Watch.RepositoryInvalid");

            RuleFor(request => request.Number)
                .GreaterThan(0)
                .WithErrorCode("PullRequest.NumberInvalid");
        }
    }
}
