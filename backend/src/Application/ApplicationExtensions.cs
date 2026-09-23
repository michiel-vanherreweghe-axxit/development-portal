using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class ApplicationExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services) =>
        services
            .AddScoped<Common.CurrentUser>()
            .AddScoped<ProvisionUser.ProvisionUser>()
            .AddScoped<CreateSignIn.CreateSignIn>()
            .AddScoped<ReadWatches.ReadWatches>()
            .AddScoped<UpdateWatches.UpdateWatches>()
            .AddScoped<ReadRepositories.ReadRepositories>()
            .AddScoped<ReadWorkflows.ReadWorkflows>()
            .AddScoped<ReadRuns.ReadRuns>()
            .AddScoped<ReadPullRequests.ReadPullRequests>()
            .AddScoped<ClosePullRequest.ClosePullRequest>()
            .AddScoped<MergePullRequest.MergePullRequest>();
}
