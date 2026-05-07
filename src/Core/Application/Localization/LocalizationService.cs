using CapitalUniversity.Core.Abstractions.Localization;
using System.Reflection;
using System.Text.Json;

namespace CapitalUniversity.Core.Application.Localization
{
    // Simple dummy class to attach resources to, if needed.
    public class SharedResource {}

    public class LocalizationService : ILocalizationService
    {
        private const string DefaultLanguage = "ar";
        private readonly ICurrentCultureService _culture;

        public LocalizationService(ICurrentCultureService culture)
        {
            _culture = culture;
        }

        public T Get<T>(string json)
        {
            var lang = _culture.Language;

            var dict = JsonSerializer.Deserialize<Dictionary<string, T>>(json);

            if (dict != null && dict.TryGetValue(lang, out var value))
                return value;

            return dict != null && dict.ContainsKey(DefaultLanguage) ? dict[DefaultLanguage] : default!;
        }

        public string Get(Enum value)
        {
            var lang = _culture.Language;

            var field = value.GetType().GetField(value.ToString());
            var attr = field?.GetCustomAttribute<LocalizedAttribute>();

            if (attr == null)
                return value.ToString();

            return lang == "ar" ? attr.Ar : attr.En;
        }

        public string GetString(string key)
        {
            // Dummy implementation of IStringLocalizer or key based lookup
            // Typically this would inject IStringLocalizer<SharedResource>
            // For now, return the key or a localized mock value based on current culture
            return key; // Pass-through if not found.
        }
    }
}
