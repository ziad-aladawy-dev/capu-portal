namespace CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
/// <summary>
/// Gets the localized string for the given enum value or deserializes the given JSON string to the specified type.
/// </summary>
public interface ILocalizationService
{
    T Get<T>(string json);
    string Get(Enum value);

    /// <summary>
    /// Resolve a strongly-typed localization key (see <c>LocalizedKeys</c>) against
    /// the current culture. Returns the key itself when no entry is registered, so
    /// the call is always safe even before a translation lands.
    /// </summary>
    string GetString(string key);
}
