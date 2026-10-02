using Microsoft.AspNetCore.Diagnostics;

namespace StoreOps.Api.Shared.Errors;

/// <summary>
/// Maps <see cref="AppError"/> to its declared status code and body. Anything else is an unexpected failure
/// and is returned as a generic 500 without leaking internals.
/// </summary>
public sealed class AppErrorExceptionHandler(ILogger<AppErrorExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ErrorResponse body;
        if (exception is AppError appError)
        {
            logger.LogInformation("Request failed with {ErrorCode}: {Message}", appError.Code, appError.Message);
            body = new ErrorResponse(appError.Code, appError.Message, appError.StatusCode, appError.Details);
        }
        else
        {
            logger.LogError(exception, "Unhandled exception while processing {Path}", httpContext.Request.Path);
            body = new ErrorResponse(ErrorCodes.InternalError, "An unexpected error occurred.", StatusCodes.Status500InternalServerError);
        }

        httpContext.Response.StatusCode = body.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(body, cancellationToken);
        return true;
    }
}
