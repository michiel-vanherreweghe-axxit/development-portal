using Application.Common;
using Ardalis.Result;
using Domain.Planning;
using Infrastructure.Database;
using Infrastructure.Services.GitHub;
using Infrastructure.Services.GitHub.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.ReadPlanItems;

public sealed class ReadPlanItems(ApplicationDbContext context, IGitHubService gitHubService, CurrentUser currentUser)
{
    private const int MaxParallelIssues = 8;

    public async Task<List<PlanItemDto>> ExecuteAsync(
        ReadPlanItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        var gitHubId = currentUser.GitHubId;

        var from = PlanItem.MondayOf(request.From);
        var to = request.To;

        var items = await context.PlanItems
            .Where(item => context.Users.Any(user => user.Id == item.UserId && user.GitHubId == gitHubId))
            .Where(item => item.Start >= from && item.Start <= to)
            .OrderBy(item => item.Start)
            .ThenBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var issues = items
            .Where(item => item.IsIssue)
            .Select(item => new IssueKey(item.Owner!, item.Repository!, item.IssueNumber!.Value))
            .Distinct()
            .ToList();

        var read = await Throttled.MapAsync(
            issues,
            MaxParallelIssues,
            async issue => (Key: issue, Result: await gitHubService.GetIssueAsync(
                issue.Owner,
                issue.Repository,
                issue.Number,
                cancellationToken)));

        var byIssue = read.ToDictionary(entry => entry.Key, entry => entry.Result);

        var dtos = new List<PlanItemDto>(items.Count);

        foreach (var item in items)
        {
            if (!item.IsIssue)
            {
                dtos.Add(PlanItemDto.From(item));
                continue;
            }

            var result = byIssue[new IssueKey(item.Owner!, item.Repository!, item.IssueNumber!.Value)];

            if (result.IsSuccess)
            {
                item.RefreshTitle(result.Value.Title);
            }

            dtos.Add(PlanItemDto.From(
                item,
                ToState(result),
                result.IsSuccess ? result.Value.HtmlUrl : null));
        }

        await SaveRefreshedTitlesAsync(cancellationToken);

        return dtos;
    }

    private static IssueState ToState(Result<GitHubIssue> result) =>
        result.IsSuccess
            ? result.Value.State.Equals("closed", StringComparison.OrdinalIgnoreCase) ? IssueState.Closed : IssueState.Open
            : result.Status == ResultStatus.NotFound ? IssueState.Unavailable : IssueState.Unknown;

    // The refresh is a courtesy to the next read: a concurrent write is not worth failing this one for.
    private async Task SaveRefreshedTitlesAsync(CancellationToken cancellationToken)
    {
        if (!context.ChangeTracker.HasChanges())
        {
            return;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
        }
    }

    private sealed record IssueKey(string Owner, string Repository, int Number);
}
