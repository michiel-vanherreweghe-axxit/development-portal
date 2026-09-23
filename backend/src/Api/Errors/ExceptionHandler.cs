using System.Diagnostics;
using Infrastructure.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api.Errors;

internal sealed class ExceptionHandler(
    ILogger<ExceptionHandler> logger,
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problemDetails = new ProblemDetails
        {
            Type = "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.1",
            Title = "Internal Server Error",
            Status = StatusCodes.Status500InternalServerError,
            Instance = httpContext.Request.Path,
            Detail = environment.IsDevelopment()
                ? exception.ToString()
                : "An unexpected error occurred.",
            Extensions = { ["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier }
        };

        var statusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        LogException(httpContext, exception, statusCode);

        httpContext.Response.StatusCode = statusCode;

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails
        });

        return true;
    }

    private void LogException(HttpContext httpContext, Exception exception, int statusCode)
    {
        var isServerError = statusCode >= 500;
        var loggedProperties = new Dictionary<string, object?>
        {
            ["UserId"] = httpContext.User.GetLogin() ?? "anonymous",
            ["QueryString"] = httpContext.Request.QueryString.HasValue ? httpContext.Request.QueryString.Value : null
        };

        using (logger.BeginScope(loggedProperties))
        {
            if (isServerError)
            {
                logger.LogError(exception, "An unhandled server error occurred");
            }
            else
            {
                logger.LogWarning("A client error occurred: {ExceptionType} - {Message}", exception.GetType().Name, exception.Message);
            }
        }
    }
}
