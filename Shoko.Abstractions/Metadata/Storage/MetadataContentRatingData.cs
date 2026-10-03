namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   A content rating to store, as part of the series or movie it rates.
/// </summary>
public sealed record MetadataContentRatingData
{
    /// <summary>
    ///   The ISO 3166-1 alpha-2 code of the country the rating is for, such as
    ///   <c>US</c>. At most 32 characters. A country may have several
    ///   ratings for an entry.
    /// </summary>
    public required string CountryCode { get; init; }

    /// <summary>
    ///   The rating, as the country's board writes it, such as <c>TV-14</c>.
    ///   At most 128 characters.
    /// </summary>
    public required string Rating { get; init; }

    /// <summary>
    ///   The code of the rating's language, such as <c>ja</c>, kept as given.
    ///   Left out, it is the main language of <see cref="CountryCode"/>: an
    ///   upper-case ISO 639-1 code, with a region where the country has its
    ///   own form, such as <c>EN-US</c> for <c>US</c> or <c>DE</c> for
    ///   <c>AT</c>. At most 32 characters.
    /// </summary>
    public string? LanguageCode { get; init; }
}
