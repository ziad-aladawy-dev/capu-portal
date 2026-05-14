using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Core.Application.CrossCutting.Localization
{
    public class SharedResource {}

    public class LocalizationService : ILocalizationService
    {
        private const string DefaultLanguage = "ar";
        private const string EnglishLanguage = "en";
        private readonly ICurrentCultureService _culture;
        private readonly ILogger<LocalizationService> _logger;

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
                return default!;

            try
            {
                var lang = _culture.Language?.ToLowerInvariant() ?? DefaultLanguage;
                var dict = JsonSerializer.Deserialize<Dictionary<string, T>>(json);

                if (dict == null)
                    return default!;

                if (dict.TryGetValue(lang, out var value))
                    return value;

                // Fallback to Default (Arabic)
                if (dict.TryGetValue(DefaultLanguage, out var defaultValue))
                    return defaultValue;

                // Fallback to English if Arabic is also missing
                if (dict.TryGetValue(EnglishLanguage, out var enValue))
                    return enValue;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize localized JSON: {Json}", json);
            }

            return default!;
        }

        public string Get(Enum value)
        {
            if (value == null) return string.Empty;

            var lang = _culture.Language?.ToLowerInvariant() ?? DefaultLanguage;
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
            // Future implementation: look up in .resx or DB
            return key;
        }
    }
}
