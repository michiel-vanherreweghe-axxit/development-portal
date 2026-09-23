using FastEndpoints;
using FluentValidation;

namespace Application.CreateSignIn;

public sealed record CreateSignInRequest
{
    // A GitHub personal access token, fine-grained or classic. It is never stored server side:
    // the session cookie carries it, encrypted.
    public string PersonalAccessToken { get; init; } = null!;

    public sealed class Validator : Validator<CreateSignInRequest>
    {
        public Validator() =>
            RuleFor(request => request.PersonalAccessToken)
                .NotEmpty()
                .WithErrorCode("SignIn.TokenRequired");
    }
}
