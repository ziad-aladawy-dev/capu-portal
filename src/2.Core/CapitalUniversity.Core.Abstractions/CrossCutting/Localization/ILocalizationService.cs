namespace CapitalUniversity.Core.Abstractions.CrossCutting.Localization;

public interface ILocalizationService
{
    T Get<T>(string json);
    string Get(Enum value);
    string GetLocalizedString(string? json);
    string GetCurrentLanguage();
    string GetString(string key);
    bool ContainsKey(string? key);
}
