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

    /// <summary>
    /// Returns <c>true</c> when <paramref name="key"/> exists in the underlying
    /// translation table. Used by cross-cutting handlers (e.g. the global
    /// exception handler) to decide whether to localize a message or pass it
    /// through verbatim.
    /// </summary>
    bool ContainsKey(string? key);

    string GetLocalizedString(string? json);
}
