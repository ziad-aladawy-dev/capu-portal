using CapitalUniversity.Core.Abstractions.Localization;

namespace CapitalUniversity.Core.Application.Localization;

public class LocalizationFormatter
{
    private readonly ILocalizationService _localization;

    public LocalizationFormatter(ILocalizationService localization)
    {
        _localization = localization;
    }

    public string L(Enum value)
        => _localization.Get(value);

    public T L<T>(string json)
        => _localization.Get<T>(json);
}