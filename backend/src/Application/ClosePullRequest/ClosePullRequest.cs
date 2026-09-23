using Application.Common;
using Ardalis.Result;
using Infrastructure.Services.GitHub;

namespace Application.ClosePullRequest;

public sealed class ClosePullRequest(IGitHubService gitHubService)
{
    public Task<Result> ExecuteAsync(PullRequestRequest request, CancellationToken cancellationToken = default) =>
        gitHubService.ClosePullRequestAsync(request.Owner, request.Repository, request.Number, cancellationToken);
}
