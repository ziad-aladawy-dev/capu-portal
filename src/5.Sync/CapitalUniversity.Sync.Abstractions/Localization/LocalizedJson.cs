using System.Text.Json;

namespace CapitalUniversity.Sync.Abstractions.Localization;

/// <summary>
/// Sync-layer twin of <c>CapitalUniversity.Core.Abstractions.CrossCutting.Localization.LocalizedJson</c>.
/// Sync modules persist display-text columns as the same canonical bilingual JSON
/// shape <c>{"ar":"…","en":"…"}</c> that the Core domain uses, so the read side
/// (Core services calling <c>ILocalizationService</c>) can decode them uniformly.
/// <para>
/// Lifted here as a sibling — rather than added via project reference — to keep
/// the sync layer's "infrastructure-isolated, references only Sync.Abstractions"
/// contract intact. The semantics MUST stay byte-equivalent to the Core helper;
/// changes here belong in both places.
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

    /// <summary>
    /// Ensure a string is always a valid bilingual JSON object <c>{"ar":"…","en":"…"}</c>.
    /// <list type="bullet">
    /// <item>If <paramref name="value"/> is already a valid bilingual JSON, it is returned in canonical shape.</item>
    /// <item>If it is a JSON object missing a key, the missing key is added with an empty string.</item>
    /// <item>If it is plain text or invalid JSON, it is treated as the value for both cultures.</item>
    /// </list>
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Of(string.Empty, string.Empty);

        try
        {
            using var doc = JsonDocument.Parse(value);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                var ar = doc.RootElement.TryGetProperty("ar", out var arProp) ? arProp.GetString() : null;
                var en = doc.RootElement.TryGetProperty("en", out var enProp) ? enProp.GetString() : null;

                if (ar != null || en != null)
                {
                    return Of(ar ?? string.Empty, en ?? string.Empty);
                }
            }
        }
        catch (JsonException)
        {
            // Not a JSON object — treat as plain text.
        }

        // Plain text or non-bilingual JSON: use the input for both cultures.
        return Of(value, value);
    }

    /// <summary>
    /// Pull a specific culture entry out of a stored <c>{"ar":"…","en":"…"}</c>
    /// blob. Returns the original input when it doesn't parse as a culture
    /// dictionary or doesn't contain the requested key — preserves backwards
    /// compatibility with plain-text legacy values.
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

    /// <summary>
    /// Required-ness check for bilingual columns. Returns <c>true</c> when:
    /// <list type="bullet">
    /// <item><paramref name="value"/> is null / whitespace.</item>
    /// <item><paramref name="value"/> parses to <c>{"ar":"…","en":"…"}</c> and BOTH culture strings are missing or blank.</item>
    /// </list>
    /// Plain-text values (legacy, non-JSON) are non-empty when the raw string is non-blank.
    /// <para>
    /// Validators that previously called <c>string.IsNullOrWhiteSpace(record.Field)</c>
    /// must switch to this helper once the corresponding mapper starts emitting
    /// normalized JSON, otherwise an empty-input row would pass validation
    /// (the JSON literal <c>{"ar":"","en":""}</c> is itself non-whitespace).
    /// </para>
    /// </summary>
    public static bool IsEmpty(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;

        try
        {
            using var doc = JsonDocument.Parse(value);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;

            var sawCultureKey = false;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.String) continue;
                sawCultureKey = true;
                var s = prop.Value.GetString();
                if (!string.IsNullOrWhiteSpace(s)) return false;
            }

            // Object had at least one culture key but every value was blank — empty.
            // Object had no string-valued keys at all — treat as non-empty (some
            // structural JSON we don't understand; let it through).
            return sawCultureKey;
        }
        catch (JsonException)
        {
            // Non-JSON / plain text: non-empty (already passed the IsNullOrWhiteSpace gate above).
            return false;
        }
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
