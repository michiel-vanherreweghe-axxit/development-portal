using Ardalis.Result;
using FastEndpoints;
using FluentValidation.Results;

namespace Api.Extensions;

public static class EndpointExtensions
{
    // Request validation is rendered by FastEndpoints itself; this covers the failures an
    // operation returns, and renders them through the same problem-details writer so both
    // shapes carry the error code the frontend translates.
    public static async Task<bool> SendIfFailedAsync<T>(this IEndpoint endpoint, Result<T> result)
    {
        if (result.IsSuccess)
        {
            return false;
        }

        if (result.Status is ResultStatus.Invalid)
        {
            var failures = result.ValidationErrors
                .Select(error => new ValidationFailure(error.Identifier, error.ErrorMessage ?? error.ErrorCode)
                {
                    ErrorCode = error.ErrorCode,
                })
                .ToList();

            await endpoint.HttpContext.Response.SendErrorsAsync(failures, StatusCodes.Status400BadRequest);

            return true;
        }

        var problem = Results.Problem(
            statusCode: ToStatusCode(result.Status),
            title: result.Errors.FirstOrDefault() ?? result.Status.ToString());

        await problem.ExecuteAsync(endpoint.HttpContext);

        return true;
    }

    private static int ToStatusCode(ResultStatus status) => status switch
    {
        ResultStatus.NotFound => StatusCodes.Status404NotFound,
        ResultStatus.Conflict => StatusCodes.Status409Conflict,
        ResultStatus.Forbidden => StatusCodes.Status403Forbidden,
        ResultStatus.Unauthorized => StatusCodes.Status401Unauthorized,
        ResultStatus.Invalid => StatusCodes.Status400BadRequest,
        ResultStatus.Unavailable => StatusCodes.Status503ServiceUnavailable,
        ResultStatus.Ok => StatusCodes.Status200OK,
        ResultStatus.Created => StatusCodes.Status201Created,
        ResultStatus.NoContent => StatusCodes.Status204NoContent,
        ResultStatus.Error => StatusCodes.Status500InternalServerError,
        ResultStatus.CriticalError => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status500InternalServerError,
    };
}
