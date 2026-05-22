using System.Globalization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using Microsoft.AspNetCore.Http;

namespace CapitalUniversity.Core.Application.CrossCutting.Localization;

/// <summary>
/// Resolves the current request's preferred language from the
/// <c>Accept-Language</c> header. The header is parsed as an RFC 7231 quality
/// list, candidates are walked in descending q-order, and the first entry
/// whose primary subtag matches a supported culture (<c>"ar"</c> or
/// <c>"en"</c>) wins. Anything unrecognized — empty header, malformed value,
/// only unsupported cultures — falls back to <c>"ar"</c>, the project's
/// default culture.
/// </summary>
public class CurrentCultureService : ICurrentCultureService
{
    private const string DefaultLanguage = "ar";
    private const string EnglishLanguage = "en";

    private readonly IHttpContextAccessor _http;

    public CurrentCultureService(IHttpContextAccessor http)
    {
        _http = http;
    }

    public string Language
    {
        get
        {
            var header = _http.HttpContext?.Request.Headers["Accept-Language"].ToString();
            if (!string.IsNullOrWhiteSpace(header))
            {
                return Resolve(header);
            }

            // Background / outbox context: no HttpContext, so fallback to the
            // ambient CultureInfo.CurrentUICulture set by SystemCultureScope.
            return Normalize(CultureInfo.CurrentUICulture.Name);
        }
    }

    /// <summary>
    /// Reduces any culture name (e.g. "en-US", "AR") to one of the two
    /// supported primary tags. Fallback is "ar" (default).
    /// </summary>
    internal static string Normalize(string? cultureName)
    {
        var primary = ToSupportedPrimaryTag(cultureName ?? string.Empty);
        return primary ?? DefaultLanguage;
    }

    /// <summary>
    /// Parse an <c>Accept-Language</c> header value into a two-letter culture
    /// tag the catalogue understands. Walks the comma-separated list, scores
    /// each candidate by its quality factor (default 1.0, q=0 entries are
    /// excluded), and returns the highest-scoring supported primary tag.
    /// </summary>
    internal static string Resolve(string? header)
    {
        if (string.IsNullOrWhiteSpace(header)) return DefaultLanguage;

        string? best = null;
        var bestQuality = -1.0;

        foreach (var entry in header.Split(','))
        {
            var trimmed = entry.Trim();
            if (trimmed.Length == 0) continue;

            // Split tag from quality factor. "ar-EG;q=0.9" → tagPart=ar-EG, q=0.9.
            string tagPart;
            var quality = 1.0;
            var semi = trimmed.IndexOf(';');
            if (semi >= 0)
            {
                tagPart = trimmed[..semi].Trim();
                quality = ParseQuality(trimmed[(semi + 1)..]);
            }
            else
            {
                tagPart = trimmed;
            }

            if (quality <= 0) continue;

            var primary = ToSupportedPrimaryTag(tagPart);
            if (primary is null) continue;

            if (quality > bestQuality)
            {
                best = primary;
                bestQuality = quality;
            }
        }

        return best ?? DefaultLanguage;
    }

    /// <summary>
    /// Pull the q-value out of <c>"q=0.9"</c>-style parameters. Defaults to 1.0
    /// when the parameter is missing, malformed, or not a <c>q=…</c> directive.
    /// </summary>
    private static double ParseQuality(string parameters)
    {
        foreach (var part in parameters.Split(';'))
        {
            var p = part.Trim();
            if (p.Length < 2) continue;
            if ((p[0] == 'q' || p[0] == 'Q') && p[1] == '=')
            {
                var q = p.AsSpan(2).Trim();
                return double.TryParse(q, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : 1.0;
            }
        }
        return 1.0;
    }

    /// <summary>
    /// Reduce a culture tag (<c>"en-US"</c>, <c>"AR"</c>, <c>"ar-EG"</c>) to
    /// the two-letter primary subtag the catalogue indexes by. Returns
    /// <c>null</c> for anything outside the supported set (including the
    /// wildcard <c>"*"</c>) so the caller can keep walking the quality list.
    /// </summary>
    private static string? ToSupportedPrimaryTag(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var span = value.AsSpan().Trim();
        var dash = span.IndexOf('-');
        var primary = (dash >= 0 ? span[..dash] : span).ToString().ToLowerInvariant();
        return primary switch
        {
            EnglishLanguage => EnglishLanguage,
            DefaultLanguage => DefaultLanguage,
            _ => null,
        };
    }
}
