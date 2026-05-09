namespace CapitalUniversity.Core.Abstractions.Localization;
/// <summary>
/// Gets the localized string for the given enum value or deserializes the given JSON string to the specified type.
/// </summary>
public interface ILocalizationService
{
    T Get<T>(string json);
    string Get(Enum value);
}
