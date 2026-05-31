using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;

namespace CapitalUniversity.Core.UniTests._Helpers;

/// <summary>
/// Stand-in <see cref="ILocalizationService"/> for unit tests. Decodes bilingual
/// JSON strings <c>{"ar":"…","en":"…"}</c> by returning the English value, or
/// the input verbatim if it's not a culture dictionary.
/// </summary>
internal sealed class TestLocalizationService : ILocalizationService
{
    public T Get<T>(string json)
    {
        if (typeof(T) == typeof(string))
        {
            return (T)(object)LocalizedJson.Extract(json, "en");
        }
        return json is T direct ? direct : default!;
    }

    public string Get(Enum value) => value.ToString();
    public string GetString(string key) => key;
    public bool ContainsKey(string? key) => false;
}