using Application.Common;
using Ardalis.Result;
using Domain.Watches;
using Infrastructure.Services.GitHub;

namespace Application.ReadWorkflows;

public sealed class ReadWorkflows(IGitHubService gitHubService)
{
    public async Task<Result<List<WorkflowDto>>> ExecuteAsync(
        ReadWorkflowsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Watch.TryParseFullName(request.Repository, out var owner, out var repository))
        {
            return Result<List<WorkflowDto>>.Invalid(new ValidationError
            {
                Identifier = nameof(request.Repository),
                ErrorCode = "Watch.RepositoryInvalid",
            });
        }

        var workflows = await gitHubService.GetWorkflowsAsync(owner, repository, cancellationToken);

        return workflows.IsSuccess
            ? Result<List<WorkflowDto>>.Success(workflows.Value.Select(GitHubMapper.ToDto).ToList())
            : Result<List<WorkflowDto>>.Error(workflows.Errors.FirstOrDefault());
    }
}
