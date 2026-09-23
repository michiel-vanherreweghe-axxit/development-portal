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

        var now = timeProvider.GetUtcNow();

        if (existing is null)
        {
            var created = User.Create(request.GitHubId, request.Login, request.AvatarUrl, now);
            if (!created.IsSuccess)
            {
                return Result.Invalid(created.ValidationErrors.ToList());
            }

            await context.Users.AddAsync(created.Value, cancellationToken);
        }
        else
        {
            var signedIn = existing.SignIn(request.Login, request.AvatarUrl, now);
            if (!signedIn.IsSuccess)
            {
                return signedIn;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
