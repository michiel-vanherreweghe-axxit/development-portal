using System.Net.Http.Headers;
using System.Text.Json;
using Infrastructure.Services.GitHub.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refit;

namespace Infrastructure.Services.GitHub.Extensions;

public static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddGitHubClient(this IServiceCollection services, IConfiguration configuration)
    {
        var gitHubConfiguration = configuration.GetSection(GitHubConfiguration.SectionName).Get<GitHubConfiguration>();
        if (gitHubConfiguration is null)
        {
            return services;
        }

        services
            .AddOptions<GitHubConfiguration>()
            .Bind(configuration.GetSection(GitHubConfiguration.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddTransient<GitHubUserTokenHandler>();

        services
            .AddRefitGeneratedClient<IGitHubApi>(RefitSettings())
            .ConfigureHttpClient((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<GitHubConfiguration>>().Value;

                client.BaseAddress = options.Uri;
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("development-portal", "1.0"));
            })
            .AddHttpMessageHandler<GitHubUserTokenHandler>()
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = gitHubConfiguration.AttemptTimeout;
                options.TotalRequestTimeout.Timeout = gitHubConfiguration.TotalTimeout;
                options.CircuitBreaker.SamplingDuration = gitHubConfiguration.AttemptTimeout * 3;
                options.Retry.ShouldHandle = GitHubRetryPolicy.ShouldHandleAsync;
                options.Retry.DelayGenerator = GitHubRetryPolicy.DelayAsync;
            });

        services.AddScoped<IGitHubService, GitHubService>();

        return services;
    }

    private static RefitSettings RefitSettings() =>
        new()
        {
            ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            }),
        };
}
