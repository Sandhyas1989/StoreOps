namespace StoreOps.Api.Shared.Errors;

/// <summary>Collects field-level validation failures and throws a single <see cref="ValidationError"/>.</summary>
public sealed class ValidationErrorBuilder
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.OrdinalIgnoreCase);

    public bool HasErrors => _errors.Count > 0;

    public ValidationErrorBuilder Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var messages))
        {
            messages = [];
            _errors[field] = messages;
        }

        messages.Add(message);
        return this;
    }

    /// <summary>
    /// Returns the trimmed value, or records <paramref name="message"/> against <paramref name="field"/> and returns
    /// an empty string when the value is missing or blank. Callers must call <see cref="ThrowIfAny"/> before using it.
    /// </summary>
    public string RequireText(string field, string? value, string message)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            Add(field, message);
            return string.Empty;
        }

        return trimmed;
    }

    public void ThrowIfAny(string message = "The request is invalid.")
    {
        if (HasErrors)
        {
            throw new ValidationError(message, _errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        }
    }
}
