using Application.Common;
using Domain.Watches;
using Infrastructure.Database;
using Infrastructure.Services.GitHub;
using Microsoft.EntityFrameworkCore;

namespace Application.ReadRuns;

public sealed class ReadRuns(ApplicationDbContext context, IGitHubService gitHubService, CurrentUser currentUser)
{
    private const int MaxParallelCalls = 8;

    public async Task<List<WatchRunsDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var gitHubId = currentUser.GitHubId;

        var user = await context.Users
            .AsNoTracking()
            .Where(user => user.GitHubId == gitHubId)
            .Select(user => new { user.Id, user.RunLimit })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return [];
        }

        var watches = await context.Watches
            .AsNoTracking()
            .Where(watch => watch.UserId == user.Id)
            .OrderBy(watch => watch.SortOrder)
            .ToListAsync(cancellationToken);

        var rows = await Throttled.MapAsync(
            watches,
            MaxParallelCalls,
            watch => ReadWatchAsync(watch, user.RunLimit, cancellationToken));

        return rows.ToList();
    }

    private async Task<WatchRunsDto> ReadWatchAsync(Watch watch, int runLimit, CancellationToken cancellationToken)
    {
        var runs = await gitHubService.GetWorkflowRunsAsync(
            watch.Owner,
            watch.Repository,
            watch.WorkflowFile,
            runLimit,
            cancellationToken);

        return new WatchRunsDto
        {
            Repository = watch.FullName,
            WorkflowFile = watch.WorkflowFile,
            DisplayName = watch.DisplayName,
            Runs = runs.IsSuccess ? runs.Value.Select(GitHubMapper.ToDto).ToList() : [],
            Error = runs.IsSuccess ? null : runs.Errors.FirstOrDefault(),
        };
    }
}
