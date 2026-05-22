using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;

namespace CapitalUniversity.Core.IntegrationsTests._Helpers;

internal sealed class TestLocalizationService : ILocalizationService
{
    public T Get<T>(string json) =>
        json is T direct ? direct : default!;

    public string Get(Enum value) => value.ToString();
    public string GetString(string key) => key;
    public bool ContainsKey(string? key) => false;
}
