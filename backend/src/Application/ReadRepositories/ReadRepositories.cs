using Application.Common;
using Ardalis.Result;
using Infrastructure.Services.GitHub;

namespace Application.ReadRepositories;

public sealed class ReadRepositories(IGitHubService gitHubService)
{
    public async Task<Result<List<RepositoryDto>>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var repositories = await gitHubService.GetRepositoriesAsync(cancellationToken);

        return repositories.IsSuccess
            ? Result<List<RepositoryDto>>.Success(repositories.Value.Select(GitHubMapper.ToDto).ToList())
            : Result<List<RepositoryDto>>.Error(repositories.Errors.FirstOrDefault());
    }
}
