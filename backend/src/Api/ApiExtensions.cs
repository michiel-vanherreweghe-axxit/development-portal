using Api.Errors;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api;

public static class ApiExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddExceptionHandler<ExceptionHandler>();
        services.AddProblemDetails();
        services.AddApiHealthChecks();

        return services;
    }

    private static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["liveness"]);

        return services;
    }
}
