using System.Text.Json.Serialization;

namespace StoreOps.Api.Shared.Errors;

/// <summary>The single error body shape returned by every endpoint.</summary>
public sealed record ErrorResponse(
    string Code,
    string Message,
    int StatusCode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string[]>? Details = null);
