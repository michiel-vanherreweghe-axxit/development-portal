using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Domain.Planning;
using Domain.Users;
using Domain.Watches;
using Infrastructure.Database;
using Infrastructure.Services.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UnitTests.Endpoints;

public sealed class PortalApiFactory : WebApplicationFactory<Api.Program>
{
    public const string UserHeader = "X-Test-User";
    public const long GitHubId = 4242;
    public const string Login = "tester";

    private long _nextGitHubId = 9000;

    private readonly string _databaseName = $"tests-{Guid.NewGuid():N}";

    public FakeGitHubService GitHub { get; } = new();

    public HttpClient CreateSignedInClient(long gitHubId = GitHubId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(UserHeader, gitHubId.ToString(CultureInfo.InvariantCulture));

        return client;
    }

    /// <summary>A fresh user with a client signed in as them, so tests sharing a factory never share rows.</summary>
    public async Task<(HttpClient Client, int UserId)> CreateUserAsync()
    {
        var gitHubId = Interlocked.Increment(ref _nextGitHubId);

        return (CreateSignedInClient(gitHubId), await SeedOtherUserAsync(gitHubId));
    }

    public async Task<int> SeedOtherUserAsync(long gitHubId)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = User.Create(gitHubId, $"other-{gitHubId}", null, DateTimeOffset.UtcNow).Value!;
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        return user.Id;
    }

    public async Task<PlanItem> SeedPlanItemAsync(PlanItem item)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.PlanItems.AddAsync(item);
        await context.SaveChangesAsync();

        return item;
    }

    public async Task<List<PlanItem>> ReadPlanItemsAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.PlanItems.AsNoTracking().ToListAsync();
    }

    public async Task<int> SeedUserAsync(params (string Repository, string WorkflowFile)[] watches)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = await context.Users.FirstOrDefaultAsync(user => user.GitHubId == GitHubId);
        if (user is null)
        {
            user = User.Create(GitHubId, Login, null, DateTimeOffset.UtcNow).Value!;
            await context.Users.AddAsync(user);
            await context.SaveChangesAsync();
        }

        var order = 0;
        foreach (var watch in watches)
        {
            await context.Watches.AddAsync(
                Watch.Create(user.Id, watch.Repository, watch.WorkflowFile, null, order++).Value!);
        }

        await context.SaveChangesAsync();

        return user.Id;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Portal:FrontendUri", "https://localhost:5173");

        // Never connected to: the DbContext below is replaced with the in-memory provider. The
        // Npgsql integration only refuses to register without a connection string to read.
        builder.UseSetting("ConnectionStrings:portal", "Host=localhost;Database=portal");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGitHubService>();
            services.AddSingleton<IGitHubService>(GitHub);

            // The api boots against postgres; the tests run on the in-memory provider, and EF
            // refuses two providers in one container. Everything EF and Npgsql registered goes,
            // pool and options configurations included, before the context is registered again.
            foreach (var descriptor in services
                .Where(descriptor => descriptor.ServiceType == typeof(ApplicationDbContext) ||
                    descriptor.ServiceType.FullName?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true ||
                    descriptor.ServiceType.FullName?.StartsWith("Npgsql", StringComparison.Ordinal) == true)
                .ToList())
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            services
                .AddAuthentication(TestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });

            services.Configure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
            });
        });
    }
}

public sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(PortalApiFactory.UserHeader, out var gitHubId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, gitHubId.ToString()),
                new Claim(ClaimTypes.Name, PortalApiFactory.Login),
            ],
            SchemeName);

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
