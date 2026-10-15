using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
/// Container object with titles.
/// </summary>
public interface IWithTitles
{
    /// <summary>
    /// The preferred or default title without the extra metadata.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// The default title as indicated by the source.
    /// </summary>
    ITitle DefaultTitle { get; }

    /// <summary>
    /// The preferred title according to the language preference in the
    /// settings, and/or any title overrides.
    /// </summary>
    ITitle? PreferredTitle { get; }

    /// <summary>
    /// All known titles.
    /// </summary>
    IReadOnlyList<ITitle> Titles { get; }

    /// <summary>
    ///   The language the entry was first made or named in, as a plain
    ///   language code such as <c>ja</c>, when known.
    /// </summary>
    string? OriginalLanguageCode { get; }

    /// <summary>
    ///   The country the entry comes from, as an ISO 3166 two-letter code
    ///   such as <c>JP</c>, when known.
    /// </summary>
    string? OriginalCountryCode { get; }

    /// <summary>
    ///   <see cref="OriginalLanguageCode"/> as a language, told apart by
    ///   <see cref="OriginalCountryCode"/> where the language splits by
    ///   country, such as <c>pt</c> in <c>BR</c>; <c>null</c> without a code.
    /// </summary>
    TitleLanguage? OriginalLanguage { get; }

    /// <summary>
    ///   The entry's title in <see cref="OriginalLanguageCode"/>, an official
    ///   one before a synonym, or <c>null</c> when the language is unknown or
    ///   it has no title in it.
    /// </summary>
    ITitle? OriginalTitle { get; }
}
