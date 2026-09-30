using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   A content rating a plugin source gave one of its stored series or
///   movies, for one country.
/// </summary>
public class Metadata_ContentRating : MetadataEntryRow, IContentRating, IMetadataStoreRow<Metadata_ContentRating>
{
    #region Database Columns

    /// <summary>
    ///   The row's ID.
    /// </summary>
    public int Metadata_ContentRatingID { get; set; }

    /// <summary>
    ///   The ISO 3166-1 alpha-2 code of the country the rating is for.
    /// </summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>
    ///   The ISO 639-1 alpha-2 code of the rating's language.
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    ///   The rating, as the country's board writes it.
    /// </summary>
    public string Rating { get; set; } = string.Empty;

    #endregion

    #region Store Row Implementation

    int IMetadataStoreRow<Metadata_ContentRating>.RowID
    {
        get => Metadata_ContentRatingID;
        set => Metadata_ContentRatingID = value;
    }

    Metadata_ContentRating IMetadataStoreRow<Metadata_ContentRating>.Clone()
        => (Metadata_ContentRating)MemberwiseClone();

    #endregion

    #region IContentRating Implementation

    TitleLanguage IContentRating.Language => LanguageCode.GetTitleLanguage();

    string IContentRating.Value => Rating;

    MetadataSource IContentRating.Source => Source;

    #endregion
}
