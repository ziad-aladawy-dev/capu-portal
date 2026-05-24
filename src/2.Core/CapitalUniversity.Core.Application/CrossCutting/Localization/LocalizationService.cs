using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Common;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Core.Application.CrossCutting.Localization
{
    public class LocalizationService : ILocalizationService
    {
        private const string DefaultLanguage = "ar";
        private const string EnglishLanguage = "en";
        private readonly ICurrentCultureService _culture;
        private readonly ILogger<LocalizationService> _logger;

        // L8 — Process-lifetime, per-culture caches for enum localizations.
        // Bounded by the total enum-value count across the codebase (low
        // hundreds at most), so memory growth is constant — no eviction
        // needed. Per-culture buckets eliminate the cross-language race a
        // single cache would have: a thread serving Arabic and a thread
        // serving English never collide on the same key.
        private static readonly ConcurrentDictionary<Enum, string> ArabicCache = new();
        private static readonly ConcurrentDictionary<Enum, string> EnglishCache = new();

        public LocalizationService(ICurrentCultureService culture, ILogger<LocalizationService> logger)
        {
            _culture = culture;
            _logger = logger;
        }

        public T Get<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                // Preserve the input shape for string callers — an empty
                // entity field stays empty rather than collapsing to null.
                // Non-string T (e.g. Dictionary<string, X>) still gets the
                // default, since "no data" can't reasonably collapse to a
                // typed value.
                if (typeof(T) == typeof(string))
                {
                    return (T)(object)(json ?? string.Empty);
                }
                return default!;
            }

            var lang = NormalizeCulture(_culture.Language);

            // Field stores the canonical {"ar":"…","en":"…"} shape. Decode and
            // resolve current culture, then fall back to default culture, then
            // to any non-empty entry, before treating the field as plain text.
            if (TryDecodeDictionary<T>(json, out var dict))
            {
                if (dict.TryGetValue(lang, out var value) && IsPresent(value))
                    return value;

                if (dict.TryGetValue(DefaultLanguage, out var defaultValue) && IsPresent(defaultValue))
                    return defaultValue;

                if (dict.TryGetValue(EnglishLanguage, out var enValue) && IsPresent(enValue))
                    return enValue;

                foreach (var anyValue in dict.Values)
                {
                    if (IsPresent(anyValue)) return anyValue;
                }
            }

            // Legacy / pre-migration data: the column holds plain text (e.g.
            // "Course Catalog" or Arabic literals seeded before the JSON shape
            // landed). Treat it as a single-culture literal and pass it
            // through when T is string — no log warning, this is expected.
            if (typeof(T) == typeof(string))
            {
                return (T)(object)json;
            }

            return default!;
        }

        public string Get(Enum value)
        {
            if (value == null) return string.Empty;

            var lang = NormalizeCulture(_culture.Language);
            var cache = lang == EnglishLanguage ? EnglishCache : ArabicCache;

            if (cache.TryGetValue(value, out var cachedValue))
                return cachedValue;

            var field = value.GetType().GetField(value.ToString());
            var attr = field?.GetCustomAttribute<LocalizedAttribute>();

            string result;
            if (attr == null)
            {
                result = value.ToString();
            }
            else
            {
                result = lang == EnglishLanguage ? attr.En : attr.Ar;
            }

            cache.TryAdd(value, result);
            return result;
        }

        public string GetString(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            return LocalizedStrings.Resolve(key, NormalizeCulture(_culture.Language));
        }

        public bool ContainsKey(string? key) => LocalizedStrings.ContainsKey(key);

        /// <summary>
        /// Reduce an Accept-Language header value (or any culture tag) to one
        /// of the two cultures the catalogue knows: <c>"ar"</c> or <c>"en"</c>.
        /// Regional variants (<c>en-US</c>, <c>ar-EG</c>) collapse to their
        /// two-letter primary tag; anything unrecognised falls back to the
        /// default culture so callers always hit a populated bucket.
        /// </summary>
        private static string NormalizeCulture(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return DefaultLanguage;

            // CurrentCultureService now normalizes the header, but the service
            // is still called directly from tests and background-task code
            // paths that hand us raw culture strings. Take the primary subtag
            // and lower-case it.
            var span = value.AsSpan().Trim();
            var dash = span.IndexOf('-');
            var primary = (dash >= 0 ? span[..dash] : span).ToString().ToLowerInvariant();
            return primary switch
            {
                EnglishLanguage => EnglishLanguage,
                DefaultLanguage => DefaultLanguage,
                _ => DefaultLanguage,
            };
        }

        private static bool IsPresent<T>(T value) =>
            value is not null && (value is not string s || !string.IsNullOrEmpty(s));

        private bool TryDecodeDictionary<T>(string json, out IReadOnlyDictionary<string, T> dict)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, T>>(json);
                if (parsed is null || parsed.Count == 0)
                {
                    dict = EmptyDict<T>.Instance;
                    return false;
                }

                // The dictionary is keyed by culture tag. Lower-case the keys
                // once so lookups against the normalized current culture are
                // O(1) and case-insensitive ("AR" / "ar" / "Ar" all match).
                var normalized = new Dictionary<string, T>(parsed.Count, StringComparer.Ordinal);
                foreach (var (key, val) in parsed)
                {
                    if (key is null) continue;
                    normalized[key.ToLowerInvariant()] = val;
                }
                dict = normalized;
                return true;
            }
            catch (JsonException)
            {
                // Plain-text / non-dictionary payload. Caller falls back to
                // literal passthrough when T is string — no warning, this is
                // expected during the migration to the JSON shape.
                dict = EmptyDict<T>.Instance;
                return false;
            }
        }

        private static class EmptyDict<T>
        {
            public static readonly IReadOnlyDictionary<string, T> Instance =
                new Dictionary<string, T>(0);
        }

        //-------
        public string GetLocalizedString(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return string.Empty;

            try
            {
                var lang = _culture.Language?.ToLowerInvariant() ?? DefaultLanguage;
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (dict == null) return string.Empty;

                if (dict.TryGetValue(lang, out var value))
                    return value;

                if (dict.TryGetValue(DefaultLanguage, out var defaultValue))
                    return defaultValue;

                if (dict.TryGetValue(EnglishLanguage, out var enValue))
                    return enValue;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize localized JSON: {Json}", json);
            }
            return string.Empty;
        }

        public string GetCurrentLanguage()
        {
            var lang = _culture.Language?.ToLowerInvariant() ?? DefaultLanguage;
            return lang == EnglishLanguage ? EnglishLanguage : DefaultLanguage;
        }
    }
}
