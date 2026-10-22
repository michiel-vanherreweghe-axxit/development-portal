using Application.Common;
using Ardalis.Result;
using Domain.Planning;
using Domain.Watches;
using Infrastructure.Database;
using Infrastructure.Services.GitHub;
using Microsoft.EntityFrameworkCore;

namespace Application.CreatePlanItem;

public sealed class CreatePlanItem(ApplicationDbContext context, IGitHubService gitHubService, CurrentUser currentUser)
{
    public async Task<Result<PlanItemDto>> ExecuteAsync(
        CreatePlanItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var gitHubId = currentUser.GitHubId;

        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.GitHubId == gitHubId, cancellationToken);
        if (user is null)
        {
            return Result<PlanItemDto>.NotFound("User.NotProvisioned");
        }

        if (request.Kind == PlanItemKind.Issue)
        {
            return await CreateIssueAsync(user.Id, request, cancellationToken);
        }

        var todo = PlanItem.CreateTodo(user.Id, request.Title, request.Description, request.Span, request.Date);

        return await SaveAsync(todo, null, null, cancellationToken);
    }

    private async Task<Result<PlanItemDto>> CreateIssueAsync(
        int userId,
        CreatePlanItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!Watch.TryParseFullName(request.Repository, out var owner, out var repository))
        {
            return Result<PlanItemDto>.Invalid(new ValidationError
            {
                Identifier = nameof(request.Repository),
                ErrorCode = "PlanItem.RepositoryInvalid",
            });
        }

        var number = request.IssueNumber!.Value;

        var start = PlanItem.StartOf(request.Span, request.Date);

        // The in-memory provider does not enforce the unique index, so the duplicate is refused here.
        var alreadyPlanned = await context.PlanItems.AnyAsync(
            item => item.UserId == userId
                && item.Owner == owner
                && item.Repository == repository
                && item.IssueNumber == number
                && item.Span == request.Span
                && item.Start == start,
            cancellationToken);
        if (alreadyPlanned)
        {
            return Result<PlanItemDto>.Conflict("PlanItem.AlreadyPlanned");
        }

        var issue = await gitHubService.GetIssueAsync(owner, repository, number, cancellationToken);
        if (!issue.IsSuccess)
        {
            return issue.Status == ResultStatus.NotFound
                ? Result<PlanItemDto>.NotFound(issue.Errors.ToArray())
                : Result<PlanItemDto>.Error(issue.Errors.FirstOrDefault() ?? GitHubErrors.RequestFailed);
        }

        if (issue.Value.PullRequest is not null)
        {
            return Result<PlanItemDto>.Invalid(new ValidationError
            {
                Identifier = nameof(request.IssueNumber),
                ErrorCode = "PlanItem.NotAnIssue",
            });
        }

        var item = PlanItem.CreateIssue(userId, request.Repository, number, issue.Value.Title, request.Span, request.Date);

        var state = issue.Value.State.Equals("closed", StringComparison.OrdinalIgnoreCase)
            ? IssueState.Closed
            : IssueState.Open;

        return await SaveAsync(item, state, issue.Value.HtmlUrl, cancellationToken);
    }

    private async Task<Result<PlanItemDto>> SaveAsync(
        Result<PlanItem> item,
        IssueState? state,
        string? url,
        CancellationToken cancellationToken)
    {
        if (!item.IsSuccess)
        {
            return Result<PlanItemDto>.Invalid(item.ValidationErrors);
        }

        await context.PlanItems.AddAsync(item.Value, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return Result<PlanItemDto>.Success(PlanItemDto.From(item.Value, state, url));
    }
}
