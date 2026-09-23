using Application.Common;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Application.ReadWatches;

public sealed class ReadWatches(ApplicationDbContext context, CurrentUser currentUser)
{
    public async Task<WatchesResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var gitHubId = currentUser.GitHubId;

        var user = await context.Users
            .AsNoTracking()
            .Where(user => user.GitHubId == gitHubId)
            .Select(user => new { user.Id, user.RunLimit })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return new WatchesResponse { RunLimit = User.DefaultRunLimit };
        }

        var watches = await context.Watches
            .AsNoTracking()
            .Where(watch => watch.UserId == user.Id)
            .OrderBy(watch => watch.SortOrder)
            .Select(watch => new WatchDto
            {
                Repository = watch.Owner + "/" + watch.Repository,
                WorkflowFile = watch.WorkflowFile,
                DisplayName = watch.DisplayName,
            })
            .ToListAsync(cancellationToken);

        return new WatchesResponse
        {
            RunLimit = user.RunLimit,
            Watches = watches,
        };
    }
}
