using Infrastructure.Database;
using Infrastructure.Services.GitHub.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public static class InfrastructureExtensions
{
    // The name of the database resource the AppHost hands over its connection string under.
    private const string DatabaseName = "portal";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, WebApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<ApplicationDbContext>(DatabaseName);

        return services
            .AddHttpContextAccessor()
            .AddGitHubClient(builder.Configuration);
    }

    /// <summary>
    /// Brings the schema up to date on boot. The portal is one process against one database, so a
    /// separate migration step would only be one more thing to forget. Tests run on the in-memory
    /// provider, which has no migrations and is skipped here.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IHost app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync();
        }
    }
}
