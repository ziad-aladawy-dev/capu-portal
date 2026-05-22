using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;

namespace CapitalUniversity.Core.IntegrationsTests._Helpers;

internal sealed class TestLocalizationService : ILocalizationService
{
    public T Get<T>(string json)
    {
        if (typeof(T) == typeof(string) && !string.IsNullOrEmpty(json))
        {
            var trimmed = json.Trim();
            if (trimmed.StartsWith("{"))
            {
                // Simple best-effort extract for english in tests.
                var match = System.Text.RegularExpressions.Regex.Match(trimmed, "[\"']en[\"']\\s*:\\s*[\"']([^\"']*)[\"']");
                if (match.Success) return (T)(object)match.Groups[1].Value;
            }
        }
        return json is T direct ? direct : default!;
    }

    public string Get(Enum value) => value.ToString();
    public string GetString(string key) => key;
    public bool ContainsKey(string? key) => false;
}
