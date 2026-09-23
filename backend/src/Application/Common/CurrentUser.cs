using Infrastructure.Authentication;
using Microsoft.AspNetCore.Http;

namespace Application.Common;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor)
{
    public long GitHubId =>
        httpContextAccessor.HttpContext?.User.GetGitHubId()
        ?? throw new InvalidOperationException("The request has no authenticated GitHub user.");

    public string Login =>
        httpContextAccessor.HttpContext?.User.GetLogin()
        ?? throw new InvalidOperationException("The request has no authenticated GitHub user.");

    public string? AvatarUrl => httpContextAccessor.HttpContext?.User.GetAvatarUrl();
}
