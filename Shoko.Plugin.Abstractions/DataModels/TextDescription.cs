using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.DataModels;

public class TextDescription
{
    public DataSourceEnum Source { get; set; }

    public TitleLanguage Language { get; set; }

    public string LanguageCode { get; set; } = string.Empty;

    public string? CountryCode { get; set; }

    public string Value { get; set; } = string.Empty;
}
