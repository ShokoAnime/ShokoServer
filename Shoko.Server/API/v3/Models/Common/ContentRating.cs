using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.API.v3.Models.Common;

public class ContentRating
{
    /// <summary>
    /// The content rating for the specified language.
    /// </summary>
    public string Rating { get; init; }

    /// <summary>
    /// The country code the rating applies for.
    /// </summary>
    public string Country { get; init; }

    /// <summary>
    /// The language code the rating applies for.
    /// </summary>
    public string Language { get; init; }

    /// <summary>
    /// The source of the content rating.
    /// </summary>
    public string Source { get; init; }

    public ContentRating(string rating, string countryCode, string languageCode, DataSourceType source)
    {
        Rating = rating;
        Country = countryCode;
        Language = languageCode;
        Source = source.ToString();
    }

    public ContentRating(TMDB_ContentRating contentRating) :
        this(contentRating.Rating, contentRating.CountryCode, contentRating.LanguageCode, DataSourceType.TMDB)
    { }
}
