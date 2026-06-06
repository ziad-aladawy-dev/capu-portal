namespace CapitalUniversity.Sync.Abstractions.Models;

/// <summary>
/// Tiny shared utilities used across the sync layer. Lifted here so the same
/// helper isn't redefined per repository/writer/filter.
/// </summary>
public static class TextHelpers
{
    /// <summary>
    /// Returns <paramref name="value"/> truncated to at most <paramref name="maxLength"/>
    /// characters. <c>null</c> passes through as <c>null</c>; empty strings as empty.
    /// Intended for clamping log/audit text to a column width (e.g. 4000-char
    /// <c>LastError</c>) — not for end-user-visible strings.
    /// </summary>
    public static string? Truncate(string? value, int maxLength)
    {
        if (value is null) return null;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}