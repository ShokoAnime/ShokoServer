using Shoko.Abstractions.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   The content rating for the specified language/country.
/// </summary>
public interface IContentRating : IMetadata
{
    /// <summary>
    /// The inferred <see cref="TitleLanguage"/> for the content rating.
    /// </summary>
    TitleLanguage Language { get; }

    /// <summary>
    /// The ISO639-1 Alpha-2 language code for the content rating.
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
}
