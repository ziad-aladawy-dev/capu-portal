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
///   <item><b>Metadata keys</b> — any key matching one of the source-generated
///       sensitive-key patterns (password, secret, token, refresh, authorization,
///       cookie, set-cookie, api[_-]?key, x-api-key) has its value replaced
///       wholesale with <c>"[REDACTED]"</c>.</item>
/// </list>
///
/// <para>
/// Conservative on purpose: redacts on any plausible match, even if it occasionally
/// over-scrubs. Losing a token-shaped substring in a log line is cheap; leaking a
/// real one is not.
/// </para>
/// </summary>
public static partial class LogScrubber
{
    public const string RedactedPlaceholder = "[REDACTED]";

    // JWT: header.payload.signature — base64url-safe chars (incl. `-` and `_`),
    // 8+ chars per segment so we don't munge inputs like "1.2.3".
    [GeneratedRegex(@"\b[A-Za-z0-9_\-]{8,}\.[A-Za-z0-9_\-]{8,}\.[A-Za-z0-9_\-]{8,}\b")]
    private static partial Regex JwtPattern();

    // "Bearer <anything-non-space>" — covers the case where the token isn't a strict
    // JWT (e.g., opaque session token shipped by a partner).
    [GeneratedRegex(@"\bBearer\s+[^\s,;]+", RegexOptions.IgnoreCase)]
    private static partial Regex BearerPattern();

    // Refresh token shape: RefreshTokenService produces 32 random bytes →
    // ~43-char base64url. Minimum 40 keeps the pattern clear of 36-char GUIDs (the
    // most common false-positive shape in metadata) while still catching anything
    // we'd actually issue as a refresh credential.
    [GeneratedRegex(@"\b[A-Za-z0-9_\-]{40,}\b")]
    private static partial Regex RefreshTokenPattern();

    [GeneratedRegex(@"password", RegexOptions.IgnoreCase)]                  private static partial Regex KeyPassword();
    [GeneratedRegex(@"secret", RegexOptions.IgnoreCase)]                    private static partial Regex KeySecret();
    [GeneratedRegex(@"^refresh", RegexOptions.IgnoreCase)]                  private static partial Regex KeyRefresh();
    [GeneratedRegex(@"token$", RegexOptions.IgnoreCase)]                    private static partial Regex KeyToken();
    [GeneratedRegex(@"authorization", RegexOptions.IgnoreCase)]             private static partial Regex KeyAuthorization();
    [GeneratedRegex(@"^(set-)?cookie$", RegexOptions.IgnoreCase)]           private static partial Regex KeyCookie();
    [GeneratedRegex(@"api[_\-]?key", RegexOptions.IgnoreCase)]              private static partial Regex KeyApiKey();
    [GeneratedRegex(@"x-api-key", RegexOptions.IgnoreCase)]                 private static partial Regex KeyXApiKey();

    private static readonly Regex[] SensitiveKeyPatterns =
    {
        KeyPassword(),
        KeySecret(),
        KeyRefresh(),
        KeyToken(),
        KeyAuthorization(),
        KeyCookie(),
        KeyApiKey(),
        KeyXApiKey(),
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
        result = BearerPattern().Replace(result, $"Bearer {RedactedPlaceholder}");
        result = JwtPattern().Replace(result, RedactedPlaceholder);
        result = RefreshTokenPattern().Replace(result, RedactedPlaceholder);
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

            scrubbed[key] = value is string s
                ? ScrubMessage(s) ?? string.Empty
                : value;
        }
        return scrubbed;
    }

    private static bool IsSensitiveKey(string key) =>
        SensitiveKeyPatterns.Any(pattern => pattern.IsMatch(key));
}
