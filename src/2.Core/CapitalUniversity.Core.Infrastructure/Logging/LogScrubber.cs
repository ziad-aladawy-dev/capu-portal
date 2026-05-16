using System.Text.RegularExpressions;

namespace CapitalUniversity.Core.Infrastructure.Logging;

/// <summary>
/// Defensive redaction for any string about to be persisted to the audit log.
///
/// <para>
/// Patterns covered (the threat model is "secret leaks into an error message
/// downstream of a service that didn't expect to be logged"):
/// </para>
/// <list type="bullet">
///   <item><b>JWT</b> — three base64url segments separated by dots, segments ≥ 8 chars.</item>
///   <item><b>Authorization header value</b> — <c>Bearer &lt;token&gt;</c>.</item>
///   <item><b>Refresh-token-shaped base64url</b> — 32+ char base64url, used by
///       <c>RefreshTokenService.GenerateRawToken</c>.</item>
///   <item><b>Metadata keys</b> — any key matching one of the
///       <see cref="SensitiveKeyPatterns"/> regexes (password, secret, token, refresh,
///       authorization, cookie, set-cookie, api[_-]?key, x-api-key) has its value
///       replaced wholesale with <c>"[REDACTED]"</c>.</item>
/// </list>
///
/// <para>
/// Conservative on purpose: redacts on any plausible match, even if it occasionally
/// over-scrubs. Losing a token-shaped substring in a log line is cheap; leaking a
/// real one is not.
/// </para>
/// </summary>
public static class LogScrubber
{
    public const string RedactedPlaceholder = "[REDACTED]";

    // JWT: header.payload.signature — base64url-safe chars (incl. `-` and `_`),
    // 8+ chars per segment so we don't munge inputs like "1.2.3".
    private static readonly Regex JwtPattern = new(
        @"\b[A-Za-z0-9_\-]{8,}\.[A-Za-z0-9_\-]{8,}\.[A-Za-z0-9_\-]{8,}\b",
        RegexOptions.Compiled);

    // "Bearer <anything-non-space>" — covers the case where the token isn't a strict
    // JWT (e.g., opaque session token shipped by a partner).
    private static readonly Regex BearerPattern = new(
        @"\bBearer\s+[^\s,;]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Refresh token shape: RefreshTokenService produces 32 random bytes →
    // ~43-char base64url. Minimum 40 keeps the pattern clear of 36-char GUIDs (the
    // most common false-positive shape in metadata) while still catching anything
    // we'd actually issue as a refresh credential.
    private static readonly Regex RefreshTokenPattern = new(
        @"\b[A-Za-z0-9_\-]{40,}\b",
        RegexOptions.Compiled);

    private static readonly Regex[] SensitiveKeyPatterns = new[]
    {
        new Regex(@"password", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"secret", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"^refresh", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"token$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"authorization", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"^(set-)?cookie$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"api[_\-]?key", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"x-api-key", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    };

    /// <summary>
    /// Scrub a free-form string. Returns the original instance if no patterns matched
    /// so common-case logging stays allocation-free.
    /// </summary>
    public static string? ScrubMessage(string? input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var result = input;
        // Bearer first so the JWT regex doesn't greedy-replace inside it.
        result = BearerPattern.Replace(result, $"Bearer {RedactedPlaceholder}");
        result = JwtPattern.Replace(result, RedactedPlaceholder);
        result = RefreshTokenPattern.Replace(result, RedactedPlaceholder);
        return result;
    }

    /// <summary>
    /// Returns a new dictionary with sensitive-keyed entries fully redacted and
    /// non-sensitive string values run through <see cref="ScrubMessage"/>. Non-string
    /// values pass through untouched. Returns null if <paramref name="metadata"/> is null.
    /// </summary>
    public static Dictionary<string, object>? ScrubMetadata(Dictionary<string, object>? metadata)
    {
        if (metadata is null || metadata.Count == 0) return metadata;

        var scrubbed = new Dictionary<string, object>(metadata.Count);
        foreach (var (key, value) in metadata)
        {
            if (IsSensitiveKey(key))
            {
                scrubbed[key] = RedactedPlaceholder;
                continue;
            }

            if (value is string s)
            {
                scrubbed[key] = ScrubMessage(s) ?? string.Empty;
            }
            else
            {
                scrubbed[key] = value;
            }
        }
        return scrubbed;
    }

    private static bool IsSensitiveKey(string key)
    {
        foreach (var pattern in SensitiveKeyPatterns)
        {
            if (pattern.IsMatch(key)) return true;
        }
        return false;
    }
}
