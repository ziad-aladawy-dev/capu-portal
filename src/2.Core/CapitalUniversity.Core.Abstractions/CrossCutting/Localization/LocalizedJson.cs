using System.Text.Json;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Localization;

/// <summary>
/// Helper for building the per-culture JSON payload that string-typed entity
/// columns (e.g. <c>Module.DisplayName</c>, <c>Resource.DisplayName</c>,
/// <c>StructureNode.Name</c>) carry. The column stays <c>nvarchar</c>; the value
/// is shaped <c>{"ar":"…","en":"…"}</c> and decoded at read time through
/// <see cref="ILocalizationService.Get{T}(string)"/>.
/// <para>
/// The output is hand-encoded (no <c>JsonSerializer</c> allocation) because
/// manifests and seeders construct these as compile-time defaults. Only the
/// two characters that JSON requires escaping inside a quoted string are
/// handled — that's enough for the Arabic / English literals used in the
/// permission manifests and the structure / curriculum seeders.
/// </para>
/// </summary>
public static class LocalizedJson
{
    /// <summary>
    /// Build <c>{"ar":"&lt;ar&gt;","en":"&lt;en&gt;"}</c>. Both values must be non-null;
    /// pass empty string explicitly when a culture has no translation yet.
    /// </summary>
    public static string Of(string ar, string en) =>
        $"{{\"ar\":\"{Escape(ar)}\",\"en\":\"{Escape(en)}\"}}";

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        // Only the two characters JSON requires inside a quoted string — the
        // Arabic / English copy in manifests never carries control bytes.
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    /// <summary>
    /// Pull a specific culture entry out of a stored
    /// <c>{"ar":"…","en":"…"}</c> blob without going through
    /// <see cref="ILocalizationService"/>. Used by seeders / migrators that
    /// need to look up rows by their English (or Arabic) form regardless of
    /// the caller's current culture. Returns the original input when it
    /// doesn't parse as a culture dictionary or doesn't contain the
    /// requested key — preserves backwards compatibility with plain-text
    /// legacy values.
    /// </summary>
    public static string Extract(string jsonOrLiteral, string culture)
    {
        if (string.IsNullOrWhiteSpace(jsonOrLiteral)) return jsonOrLiteral;
        try
        {
            using var doc = JsonDocument.Parse(jsonOrLiteral);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return jsonOrLiteral;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(prop.Name, culture, StringComparison.OrdinalIgnoreCase)
                    && prop.Value.ValueKind == JsonValueKind.String)
                {
                    return prop.Value.GetString() ?? jsonOrLiteral;
                }
            }
        }
        catch (JsonException)
        {
            // Plain-text legacy value — fall through.
        }
        return jsonOrLiteral;
    }
}
