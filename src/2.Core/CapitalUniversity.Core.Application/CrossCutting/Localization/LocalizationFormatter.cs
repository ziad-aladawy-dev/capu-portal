using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Localization;

namespace CapitalUniversity.Core.Application.CrossCutting.Localization;

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