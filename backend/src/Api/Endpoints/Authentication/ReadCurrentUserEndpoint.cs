using FastEndpoints;
using Infrastructure.Authentication;

namespace Api.Endpoints.Authentication;

public sealed record CurrentUserResponse
{
    public long GitHubId { get; init; }

    public string Login { get; init; } = null!;

    public string? AvatarUrl { get; init; }
}

public sealed class ReadCurrentUserEndpoint : EndpointWithoutRequest<CurrentUserResponse>
{
    public override void Configure()
    {
        Get("auth/me");
        Version(1);
        Summary(s => s.Summary = "Read the signed-in user");
    }

    // Everything here is already on the session cookie, so this never touches the database.
    public override Task HandleAsync(CancellationToken ct) =>
        Send.OkAsync(
            new CurrentUserResponse
            {
                GitHubId = User.GetGitHubId()!.Value,
                Login = User.GetLogin()!,
                AvatarUrl = User.GetAvatarUrl(),
            },
            ct);
}
