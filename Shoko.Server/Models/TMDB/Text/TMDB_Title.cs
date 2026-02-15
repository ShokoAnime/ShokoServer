using System;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_Title : IEquatable<TMDB_Title>, ITitle
{
    public int TMDB_TitleID { get; set; }

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

    public TMDB_Title() { }

    public TMDB_Title(ForeignEntityType parentType, int parentId, string value, string languageCode, string countryCode)
    {
        ParentType = parentType;
        ParentID = parentId;
        Value = value;
        LanguageCode = languageCode;
        CountryCode = countryCode;
    }

    public override int GetHashCode()
    {
        var hash = 17;

        hash = hash * 31 + ParentID.GetHashCode();
        hash = hash * 31 + ParentType.GetHashCode();
        hash = hash * 31 + (LanguageCode?.GetHashCode() ?? 0);
        hash = hash * 31 + (CountryCode?.GetHashCode() ?? 0);
        hash = hash * 31 + Value.GetHashCode();

        return hash;
    }

    public override bool Equals(object? other) =>
        other is not null && other is TMDB_Title title && Equals(title);

    public bool Equals(TMDB_Title? other) =>
        other != null &&
        Value == other.Value &&
        LanguageCode == other.LanguageCode &&
        CountryCode == other.CountryCode;

    public bool Equals(IText? other)
        => IText.Equals(this, other);

    public bool Equals(ITitle? other)
        => ITitle.Equals(this, other);

    #region IMetadata Implementation

    DataSource IMetadata.Source => DataSource.TMDB;

    #endregion

    #region ITitle Implementation

    TitleType ITitle.Type => TitleType.Official;

    #endregion
}
