using Application.Common;
using Ardalis.Result;
using Domain.Planning;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Application.UpdatePlanItem;

public sealed class UpdatePlanItem(ApplicationDbContext context, CurrentUser currentUser)
{
    public async Task<Result<PlanItemDto>> ExecuteAsync(
        UpdatePlanItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var gitHubId = currentUser.GitHubId;

        // Another user's row is a 404, like a missing one, so its existence does not leak.
        var item = await context.PlanItems
            .Where(item => context.Users.Any(user => user.Id == item.UserId && user.GitHubId == gitHubId))
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken);
        if (item is null)
        {
            return Result<PlanItemDto>.NotFound("PlanItem.NotFound");
        }

        // An issue entry has no text of its own; sending some is refused rather than dropped.
        if (!item.IsIssue || request.Title is not null || request.Description is not null)
        {
            var edited = item.Edit(request.Title, request.Description);
            if (!edited.IsSuccess)
            {
                return Result<PlanItemDto>.Invalid(edited.ValidationErrors);
            }
        }

        var start = PlanItem.StartOf(request.Span, request.Date);

        // The in-memory provider does not enforce the unique index, so the duplicate is refused here.
        if (item.IsIssue && await IsPlannedElsewhereAsync(item, request.Span, start, cancellationToken))
        {
            return Result<PlanItemDto>.Conflict("PlanItem.AlreadyPlanned");
        }

        item.MoveTo(request.Span, request.Date);
        item.SetCompleted(request.IsCompleted);

        await context.SaveChangesAsync(cancellationToken);

        return Result<PlanItemDto>.Success(PlanItemDto.From(item));
    }

    private Task<bool> IsPlannedElsewhereAsync(
        PlanItem item,
        PlanSpan span,
        DateOnly start,
        CancellationToken cancellationToken) =>
        context.PlanItems.AnyAsync(
            other => other.Id != item.Id
                && other.UserId == item.UserId
                && other.Owner == item.Owner
                && other.Repository == item.Repository
                && other.IssueNumber == item.IssueNumber
                && other.Span == span
                && other.Start == start,
            cancellationToken);
}
