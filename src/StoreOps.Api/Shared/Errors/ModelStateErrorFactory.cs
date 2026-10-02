using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace StoreOps.Api.Shared.Errors;

/// <summary>
/// Converts model-binding failures (malformed JSON, bad route ids, unknown enum values) into the standard
/// <see cref="ErrorResponse"/> shape so clients see one error contract. Raw JSON parser and converter messages
/// are replaced, because they expose parser internals and CLR type names.
/// </summary>
public static class ModelStateErrorFactory
{
    public const string BodyField = "body";
    public const string BodyInvalidMessage = "The request body is missing or is not valid JSON.";
    public const string ValueInvalidMessage = "The value is invalid.";

    public static IActionResult Create(ActionContext context)
    {
        var bodyParameters = context.ActionDescriptor.Parameters
            .Where(p => p.BindingInfo?.BindingSource == BindingSource.Body)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var details = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, entry) in context.ModelState)
        {
            if (entry is null || entry.Errors.Count == 0)
            {
                continue;
            }

            var (field, messages) = Describe(key, entry, bodyParameters);
            if (!details.TryGetValue(field, out var existing))
            {
                existing = [];
                details[field] = existing;
            }

            existing.AddRange(messages.Where(m => !existing.Contains(m)));
        }

        var body = new ErrorResponse(
            ErrorCodes.ValidationFailed,
            "The request is invalid.",
            StatusCodes.Status400BadRequest,
            details.ToDictionary(d => d.Key, d => d.Value.ToArray()));
        return new BadRequestObjectResult(body);
    }

    private static (string Field, IEnumerable<string> Messages) Describe(string key, ModelStateEntry entry, HashSet<string> bodyParameters)
    {
        // Missing body, malformed JSON, or the body parameter itself failing to bind.
        if (key.Length == 0 || key == "$" || bodyParameters.Contains(key))
        {
            return (BodyField, [BodyInvalidMessage]);
        }

        // A JSON path to one property, e.g. "$.priority" for an unknown enum value.
        if (key.StartsWith("$.", StringComparison.Ordinal))
        {
            return (key[2..], [ValueInvalidMessage]);
        }

        return (key, entry.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? ValueInvalidMessage : e.ErrorMessage).Distinct());
    }
}
