using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;

namespace CapitalUniversity.Core.UniTests._Helpers;

/// <summary>
/// Stand-in <see cref="ILocalizationService"/> for unit tests that don't
/// exercise localization decoding. The bilingual contract is "JSON dict or
/// literal passthrough" — this fake mirrors that for strings (returns the
/// input verbatim) and reflects enum names. Tests asserting on Localize
/// behavior should construct a real <c>LocalizationService</c> with a
/// scripted culture instead.
/// </summary>
internal sealed class TestLocalizationService : ILocalizationService
{
    public T Get<T>(string json) =>
        json is T direct ? direct : default!;

    public string Get(Enum value) => value.ToString();
    public string GetString(string key) => key;
    public bool ContainsKey(string? key) => false;
}
