using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   The content rating for the specified language/country.
/// </summary>
public interface IContentRating
{
    /// <summary>
    /// The inferred <see cref="TitleLanguage"/> for the content rating.
    /// </summary>
    TitleLanguage Language { get; }

    /// <summary>
    ///   The language code for the content rating: an ISO 639-1 alpha-2 code,
    ///   which may carry a region, such as <c>ja</c>, <c>EN-US</c> or
    ///   <c>PT-BR</c>. Its case is not fixed, so compare it ignoring case, or
    ///   use <see cref="Language"/>.
    /// </summary>
    string LanguageCode { get; }

    /// <summary>
    /// The ISO3166 Alpha-2 country code for the content rating.
    /// </summary>
    string CountryCode { get; }

    /// <summary>
    /// The content rating for the country code.
    /// </summary>
    string Value { get; }

    /// <summary>
    ///   The source of the content rating.
    /// </summary>
    MetadataSource Source { get; }
}
