namespace StoreOps.Api.Shared.Errors;

/// <summary>
/// Base type for expected application failures. Services throw a subtype; <see cref="AppErrorExceptionHandler"/>
/// converts it into an <see cref="ErrorResponse"/>. Unexpected failures are never modelled as an AppError.
/// </summary>
public abstract class AppError(string code, string message, int statusCode, IReadOnlyDictionary<string, string[]>? details = null)
    : Exception(message)
{
    public string Code { get; } = code;

    public int StatusCode { get; } = statusCode;

    public IReadOnlyDictionary<string, string[]>? Details { get; } = details;
}

/// <summary>The request is well-formed but its content is invalid (400).</summary>
public sealed class ValidationError(string message, IReadOnlyDictionary<string, string[]>? details = null)
    : AppError(ErrorCodes.ValidationFailed, message, StatusCodes.Status400BadRequest, details);

/// <summary>The addressed resource does not exist (404).</summary>
public sealed class NotFoundError(string code, string message)
    : AppError(code, message, StatusCodes.Status404NotFound);

/// <summary>The request conflicts with the current state of the resource (409).</summary>
public sealed class ConflictError(string code, string message)
    : AppError(code, message, StatusCodes.Status409Conflict);
