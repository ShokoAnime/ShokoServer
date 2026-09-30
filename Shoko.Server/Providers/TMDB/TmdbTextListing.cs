using System;
using System.Collections.Generic;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Services;

namespace Shoko.Server.Providers.TMDB;

/// <summary>
///   Works out which of the titles or overviews TMDB lists for an entity are
///   stored, and where, beside the English text kept on the entity's row.
/// </summary>
internal static class TmdbTextListing
{
    #region Planning

    /// <summary>
    ///   Puts what TMDB lists now in the order it was listed before, and takes
    ///   out what is not stored.
    /// </summary>
    /// <remarks>
    ///   Texts already stored keep their place, the English default included
    ///   where it was listed, and new ones follow in TMDB's order, each
    ///   language and country once. The American English text equal to the
    ///   default on the row is not stored; the returned gap says where it
    ///   sits. For an episode, a generic title with its own number, such as
    ///   <c>Episode 5</c>, is not stored either.
    /// </remarks>
    /// <param name="stored">The entity's stored texts from TMDB, in order.</param>
    /// <param name="englishListed">Whether the English default was listed, at the first gap in the stored positions.</param>
    /// <param name="listed">What TMDB lists now, in its order.</param>
    /// <param name="english">The English default on the entity's row.</param>
    /// <param name="episodeNumber">The episode's number, for an episode's titles, or <c>null</c>.</param>
    /// <returns>The texts to store, in order, and the position of the English default, or <c>null</c> when it is not listed.</returns>
    internal static (IReadOnlyList<ListedText> Texts, int? Gap) Plan<TText>(
        IReadOnlyList<TText> stored,
        bool englishListed,
        IReadOnlyList<ListedText> listed,
        string? english,
        int? episodeNumber = null
    ) where TText : IText
    {
        var before = new List<(string, string)>(stored.Count + 1);
        foreach (var text in stored)
        {
            if (englishListed && text.Ordering != before.Count)
            {
                before.Add(("en", "US"));
                englishListed = false;
            }

            before.Add((text.LanguageCode, text.CountryCode ?? string.Empty));
        }

        if (englishListed)
            before.Add(("en", "US"));

        var now = new Dictionary<(string, string), ListedText>();
        foreach (var text in listed)
            now.TryAdd((text.LanguageCode, text.CountryCode), text);

        var ordered = new List<ListedText>(now.Count);
        foreach (var slot in before)
            if (now.Remove(slot, out var text))
                ordered.Add(text);
        foreach (var text in listed)
            if (now.Remove((text.LanguageCode, text.CountryCode)))
                ordered.Add(text);

        var texts = new List<ListedText>(ordered.Count);
        int? gap = null;
        foreach (var text in ordered)
        {
            if (gap is null && IsEnglishDefault(text.LanguageCode, text.CountryCode, text.Value, english))
            {
                gap = texts.Count;
                continue;
            }

            if (episodeNumber is { } number && GenericEpisodeTitles.IsGeneric(text.Value, EpisodeType.Episode, number))
                continue;

            texts.Add(text);
        }

        return (texts, gap);
    }

    /// <summary>
    ///   Whether a listed text is the American English one equal to the
    ///   English default on the entity's row.
    /// </summary>
    /// <param name="languageCode">The text's language code.</param>
    /// <param name="countryCode">The text's country code.</param>
    /// <param name="value">The text.</param>
    /// <param name="english">The English default on the entity's row.</param>
    /// <returns><c>true</c> when it is.</returns>
    internal static bool IsEnglishDefault(string languageCode, string? countryCode, string value, string? english)
        => languageCode == "en" && countryCode == "US" && !string.IsNullOrEmpty(english) && string.Equals(value, english, StringComparison.Ordinal);

    #endregion

    #region Texts

    /// <summary>
    ///   The language of a TMDB translation, from its codes.
    /// </summary>
    /// <param name="languageCode">The language code.</param>
    /// <param name="countryCode">The country code, which may be empty.</param>
    /// <returns>The language.</returns>
    internal static TitleLanguage Language(string languageCode, string? countryCode)
        => string.IsNullOrEmpty(languageCode) ? TitleLanguage.None : string.IsNullOrEmpty(countryCode) ? languageCode.GetTitleLanguage() : languageCode.GetTitleLanguage(countryCode);

    /// <summary>
    ///   A listed title as it is stored.
    /// </summary>
    /// <param name="text">The listed title.</param>
    /// <returns>The title, as an official one from TMDB.</returns>
    internal static ITitle ToTitle(ListedText text)
        => new TitleStub
        {
            Source = MetadataSource.TMDB,
            Language = Language(text.LanguageCode, text.CountryCode),
            LanguageCode = text.LanguageCode,
            CountryCode = text.CountryCode,
            Value = text.Value,
            Type = TitleType.Official,
        };

    /// <summary>
    ///   A listed overview as it is stored.
    /// </summary>
    /// <param name="text">The listed overview.</param>
    /// <returns>The overview, from TMDB.</returns>
    internal static IText ToOverview(ListedText text)
        => new TextStub
        {
            Source = MetadataSource.TMDB,
            Language = Language(text.LanguageCode, text.CountryCode),
            LanguageCode = text.LanguageCode,
            CountryCode = text.CountryCode,
            Value = text.Value,
        };

    /// <summary>
    ///   A title or overview TMDB lists for an entity.
    /// </summary>
    /// <param name="LanguageCode">The language code, in lower case.</param>
    /// <param name="CountryCode">The country code, in upper case.</param>
    /// <param name="Value">The text.</param>
    internal readonly record struct ListedText(string LanguageCode, string CountryCode, string Value);

    #endregion
}
