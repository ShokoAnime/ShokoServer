using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_Overview
{
    public int TMDB_OverviewID { get; set; }

    public int ParentID { get; set; }

    public ForeignEntityType ParentType { get; set; }

    public TitleLanguage Language
    {
        get => string.IsNullOrEmpty(LanguageCode) ? TitleLanguage.None : string.IsNullOrEmpty(CountryCode) ? LanguageCode.GetTitleLanguage() : LanguageCode.GetTitleLanguage(CountryCode);
    }

    /// <summary>
    /// ISO 639-1 alpha-2 language code.
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// ISO 3166-1 alpha-2 country code.
    /// </summary>
    public string CountryCode { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public TMDB_Overview() { }

    public TMDB_Overview(ForeignEntityType parentType, int parentId, string value, string languageCode, string countryCode)
    {
        ParentType = parentType;
        ParentID = parentId;
        Value = value;
        LanguageCode = languageCode;
        CountryCode = countryCode;
    }
}
