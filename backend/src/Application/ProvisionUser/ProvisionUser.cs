using Ardalis.Result;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Application.ProvisionUser;

public sealed class ProvisionUser(ApplicationDbContext context, TimeProvider timeProvider)
{
    public async Task<Result> ExecuteAsync(ProvisionUserRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await context.Users
            .FirstOrDefaultAsync(user => user.GitHubId == request.GitHubId, cancellationToken);

        if (existing is not null)
        {
            return await SignInAsync(existing, request, cancellationToken);
        }

        var created = User.Create(request.GitHubId, request.Login, request.AvatarUrl, timeProvider.GetUtcNow());
        if (!created.IsSuccess)
        {
            return Result.Invalid(created.ValidationErrors.ToList());
        }

        await context.Users.AddAsync(created.Value, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two first sign-ins at once: the unique index on GitHubId lets one insert through,
            // and the loser signs in on the row the winner wrote instead of failing.
            context.Entry(created.Value).State = EntityState.Detached;

            var winner = await context.Users
                .FirstOrDefaultAsync(user => user.GitHubId == request.GitHubId, cancellationToken);

            if (winner is null)
            {
                throw;
            }

            return await SignInAsync(winner, request, cancellationToken);
        }

        return Result.Success();
    }

    private async Task<Result> SignInAsync(User user, ProvisionUserRequest request, CancellationToken cancellationToken)
    {
        var signedIn = user.SignIn(request.Login, request.AvatarUrl, timeProvider.GetUtcNow());
        if (!signedIn.IsSuccess)
        {
            return signedIn;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
