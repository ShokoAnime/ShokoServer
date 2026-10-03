using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Metadata;
using Shoko.Server.API.Converters;

namespace Shoko.Server.API.v3.Models.Common;

public class ContentRating
{
    /// <summary>
    /// The content rating for the specified language.
    /// </summary>
    [Required]
    public string Rating { get; init; }

    /// <summary>
    /// The country code the rating applies for.
    /// </summary>
    [Required]
    public string Country { get; init; }

    /// <summary>
    /// The language code the rating applies for.
    /// </summary>
    [Required]
    public string Language { get; init; }

    /// <summary>
    /// The source of the content rating.
    /// </summary>
    [Required]
    public string Source { get; init; }

    public ContentRating(string rating, string countryCode, string languageCode, DataSourceType source)
    {
        Rating = rating;
        Country = countryCode;
        Language = languageCode;
        Source = source.ToString();
    }

    /// <summary>
    /// A content rating of any source.
    /// </summary>
    /// <param name="contentRating">The content rating.</param>
    public ContentRating(IContentRating contentRating)
    {
        Rating = contentRating.Value;
        Country = contentRating.CountryCode;
        Language = contentRating.LanguageCode;
        Source = LegacyMetadataSpellings.Of(contentRating.Source);
    }
}
