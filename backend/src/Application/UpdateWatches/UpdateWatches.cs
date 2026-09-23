using Application.Common;
using Ardalis.Result;
using Domain.Watches;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Application.UpdateWatches;

public sealed class UpdateWatches(ApplicationDbContext context, CurrentUser currentUser)
{
    public async Task<Result<WatchesResponse>> ExecuteAsync(
        UpdateWatchesRequest request,
        CancellationToken cancellationToken = default)
    {
        var gitHubId = currentUser.GitHubId;

        var user = await context.Users.FirstOrDefaultAsync(user => user.GitHubId == gitHubId, cancellationToken);
        if (user is null)
        {
            return Result<WatchesResponse>.NotFound("User.NotProvisioned");
        }

        // The request validator has already rejected anything out of range, so a domain
        // failure here means the edge was bypassed: an error, not a 400.
        if (!user.UpdateRunLimit(request.RunLimit).IsSuccess)
        {
            return Result<WatchesResponse>.Error("User.RunLimitOutOfRange");
        }

        var watches = new List<Watch>(request.Watches.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dto in request.Watches)
        {
            // The in-memory provider does not enforce the unique index, so duplicates are dropped here.
            if (!seen.Add($"{dto.Repository}/{dto.WorkflowFile}"))
            {
                continue;
            }

            var watch = Watch.Create(user.Id, dto.Repository, dto.WorkflowFile, dto.DisplayName, watches.Count);
            if (!watch.IsSuccess)
            {
                return Result<WatchesResponse>.Error("Watch.Invalid");
            }

            watches.Add(watch.Value);
        }

        context.Watches.RemoveRange(await context.Watches
            .Where(watch => watch.UserId == user.Id)
            .ToListAsync(cancellationToken));

        await context.Watches.AddRangeAsync(watches, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return Result<WatchesResponse>.Success(new WatchesResponse
        {
            RunLimit = user.RunLimit,
            Watches = watches
                .Select(watch => new WatchDto
                {
                    Repository = watch.FullName,
                    WorkflowFile = watch.WorkflowFile,
                    DisplayName = watch.DisplayName,
                })
                .ToList(),
        });
    }
}
