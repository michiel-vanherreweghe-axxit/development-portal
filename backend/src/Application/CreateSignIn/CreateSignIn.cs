using Application.ProvisionUser;
using Ardalis.Result;
using Infrastructure.Services.GitHub;

namespace Application.CreateSignIn;

public sealed class CreateSignIn(IGitHubService gitHubService, ProvisionUser.ProvisionUser provisionUser)
{
    public async Task<Result<CreateSignInResponse>> ExecuteAsync(
        CreateSignInRequest request,
        CancellationToken cancellationToken = default)
    {
        // GitHub answering for the token is what proves it is usable, and tells us who it belongs to.
        var account = await gitHubService.GetAccountAsync(request.PersonalAccessToken, cancellationToken);
        if (!account.IsSuccess)
        {
            // GitHub not answering is not the same as GitHub refusing the token: telling the user
            // to make a new one would send them after a token that is perfectly fine.
            return account.Status == ResultStatus.Unauthorized
                ? Result<CreateSignInResponse>.Invalid(new ValidationError
                {
                    Identifier = nameof(request.PersonalAccessToken),
                    ErrorCode = "SignIn.TokenInvalid",
                })
                : Result<CreateSignInResponse>.Error(GitHubErrors.RequestFailed);
        }

        var provisioned = await provisionUser.ExecuteAsync(
            new ProvisionUserRequest
            {
                GitHubId = account.Value.Id,
                Login = account.Value.Login,
                AvatarUrl = account.Value.AvatarUrl,
            },
            cancellationToken);

        return provisioned.IsSuccess
            ? Result<CreateSignInResponse>.Success(new CreateSignInResponse
            {
                GitHubId = account.Value.Id,
                Login = account.Value.Login,
                AvatarUrl = account.Value.AvatarUrl,
                AccessToken = request.PersonalAccessToken,
            })
            : Result<CreateSignInResponse>.Error("User.NotProvisioned");
    }
}
