using Application.Common;
using Ardalis.Result;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Application.DeletePlanItem;

public sealed class DeletePlanItem(ApplicationDbContext context, CurrentUser currentUser)
{
    public async Task<Result> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        var gitHubId = currentUser.GitHubId;

        var item = await context.PlanItems
            .Where(item => context.Users.Any(user => user.Id == item.UserId && user.GitHubId == gitHubId))
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (item is null)
        {
            return Result.NotFound("PlanItem.NotFound");
        }

        context.PlanItems.Remove(item);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
