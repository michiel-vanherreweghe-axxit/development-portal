using Application.Common;
using Ardalis.Result;
using Domain.Watches;
using Infrastructure.Services.GitHub;

namespace Application.ReadIssues;

public sealed class ReadIssues(IGitHubService gitHubService)
{
    public async Task<Result<List<IssueDto>>> ExecuteAsync(
        ReadIssuesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Watch.TryParseFullName(request.Repository, out var owner, out var repository))
        {
            return Result<List<IssueDto>>.Invalid(new ValidationError
            {
                Identifier = nameof(request.Repository),
                ErrorCode = "PlanItem.RepositoryInvalid",
            });
        }

        var issues = await gitHubService.SearchIssuesAsync(owner, repository, request.Query?.Trim() ?? string.Empty, cancellationToken);

        return issues.IsSuccess
            ? Result<List<IssueDto>>.Success(issues.Value.Select(GitHubMapper.ToDto).ToList())
            : Result<List<IssueDto>>.Error(issues.Errors.FirstOrDefault() ?? GitHubErrors.RequestFailed);
    }
}
