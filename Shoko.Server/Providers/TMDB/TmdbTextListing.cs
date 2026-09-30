using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Services;
using TMDbLib.Objects.General;

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
    ///   language and country once, synonyms apart from translations. The
    ///   American English text equal to the default on the row is not
    ///   stored; the returned gap says where it sits. For an episode, a
    ///   generic title with its own number is not stored either.
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
        var before = new List<(string, string, bool)>(stored.Count + 1);
        foreach (var text in stored)
        {
            if (englishListed && text.Ordering != before.Count)
            {
                before.Add(("en", "US", false));
                englishListed = false;
            }

            before.Add((text.LanguageCode, text.CountryCode ?? string.Empty, text is ITitle { Type: TitleType.Synonym }));
        }

        if (englishListed)
            before.Add(("en", "US", false));

        var now = new Dictionary<(string, string, bool), ListedText>();
        foreach (var text in listed)
            now.TryAdd(SlotOf(text), text);

        var ordered = new List<ListedText>(now.Count);
        foreach (var slot in before)
            if (now.Remove(slot, out var text))
                ordered.Add(text);
        foreach (var text in listed)
            if (now.Remove(SlotOf(text)))
                ordered.Add(text);

        var texts = new List<ListedText>(ordered.Count);
        int? gap = null;
        foreach (var text in ordered)
        {
            if (gap is null && text.Type is not TitleType.Synonym && IsEnglishDefault(text.LanguageCode, text.CountryCode, text.Value, english))
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
    ///   Where a listed text goes: its language, its country and whether it
    ///   is a synonym.
    /// </summary>
    /// <param name="text">The listed text.</param>
    /// <returns>The slot.</returns>
    private static (string, string, bool) SlotOf(ListedText text)
        => (text.LanguageCode, text.CountryCode, text.Type is TitleType.Synonym);

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

    #region Transcriptions

    /// <summary>
    ///   The alternative title types that name a transcription system, and
    ///   the language each one writes.
    /// </summary>
    private static readonly Dictionary<string, TitleLanguage> _transcriptionSystems = new(StringComparer.Ordinal)
    {
        ["pinyin"] = TitleLanguage.Pinyin,
        ["hanyu pinyin"] = TitleLanguage.Pinyin,
        ["拼音"] = TitleLanguage.Pinyin,
        ["romanized chinese name"] = TitleLanguage.Pinyin,
        ["hepburn"] = TitleLanguage.Romaji,
        ["hepburn romanization"] = TitleLanguage.Romaji,
        ["hepburn romanisation"] = TitleLanguage.Romaji,
        ["rr"] = TitleLanguage.KoreanTranscription,
        ["romaja"] = TitleLanguage.KoreanTranscription,
        ["revised romanization"] = TitleLanguage.KoreanTranscription,
        ["revised romanisation"] = TitleLanguage.KoreanTranscription,
    };

    /// <summary>
    ///   The alternative title types that say a title is transcribed, but not
    ///   from which language.
    /// </summary>
    private static readonly HashSet<string> _genericTranscriptions = new(StringComparer.Ordinal)
    {
        "romaji",
        "romanized",
        "romanised",
        "romanization",
        "romanisation",
        "romanized title",
        "transliteration",
    };

    /// <summary>
    ///   The transcriptions among the alternative titles TMDB lists for a
    ///   show or a movie, at most one per transcription language.
    /// </summary>
    /// <remarks>
    ///   A type naming its system decides the language. A generic type takes
    ///   the entity's original language, and only in that language's home
    ///   countries. Only Latin text counts. Home-country and plain ASCII
    ///   titles go first, then TMDB's order. The language is a guess, so each
    ///   is a synonym, never an official title.
    /// </remarks>
    /// <param name="alternativeTitles">The alternative titles TMDB lists, or <c>null</c>.</param>
    /// <param name="originalLanguageCode">The entity's original language code, or <c>null</c>.</param>
    /// <returns>The transcribed titles, as synonyms without a country, in TMDB's order.</returns>
    internal static IReadOnlyList<ListedText> TranscribedTitles(IEnumerable<AlternativeTitle>? alternativeTitles, string? originalLanguageCode)
    {
        var fallback = TranscriptionOf(originalLanguageCode);
        var candidates = new List<(TitleLanguage Language, bool Home, bool Ascii, int Index, string Value)>();
        var index = 0;
        foreach (var title in alternativeTitles ?? [])
        {
            var position = index++;
            var value = title.Title?.Trim();
            if (string.IsNullOrEmpty(value) || !IsLatin(value))
                continue;

            var type = NormalizeType(title.Type);
            var country = title.Iso_3166_1?.Trim().ToUpperInvariant();
            if (!_transcriptionSystems.TryGetValue(type, out var language))
            {
                if (fallback is not { } generic || !_genericTranscriptions.Contains(type) || !IsHomeCountry(generic, country))
                    continue;

                language = generic;
            }

            candidates.Add((language, IsHomeCountry(language, country), value.All(char.IsAscii), position, value));
        }

        return
        [
            .. candidates
                .GroupBy(candidate => candidate.Language)
                .Select(group => group
                    .OrderByDescending(candidate => candidate.Home)
                    .ThenByDescending(candidate => candidate.Ascii)
                    .ThenBy(candidate => candidate.Index)
                    .First())
                .OrderBy(candidate => candidate.Index)
                .Select(candidate => new ListedText(candidate.Language.GetString(), string.Empty, candidate.Value, TitleType.Synonym)),
        ];
    }

    /// <summary>
    ///   The transcription language of an original language.
    /// </summary>
    /// <param name="originalLanguageCode">The original language code, or <c>null</c>.</param>
    /// <returns>The language, or <c>null</c> when it has none we store.</returns>
    private static TitleLanguage? TranscriptionOf(string? originalLanguageCode)
        => originalLanguageCode?.Trim().ToLowerInvariant() switch
        {
            "ja" => TitleLanguage.Romaji,
            "zh" or "cn" => TitleLanguage.Pinyin,
            "ko" => TitleLanguage.KoreanTranscription,
            _ => null,
        };

    /// <summary>
    ///   Whether a country is a home of the language a transcription is from.
    /// </summary>
    /// <param name="language">The transcription language.</param>
    /// <param name="countryCode">The country code, in upper case, or <c>null</c>.</param>
    /// <returns><c>true</c> when it is.</returns>
    private static bool IsHomeCountry(TitleLanguage language, string? countryCode)
        => language switch
        {
            TitleLanguage.Romaji => countryCode is "JP",
            TitleLanguage.Pinyin => countryCode is "CN" or "TW" or "HK" or "MO" or "SG",
            TitleLanguage.KoreanTranscription => countryCode is "KR",
            _ => false,
        };

    /// <summary>
    ///   An alternative title type, trimmed and in lower case, without its
    ///   zero-width characters and with its spaces collapsed.
    /// </summary>
    /// <param name="type">The type, or <c>null</c>.</param>
    /// <returns>The normalized type.</returns>
    private static string NormalizeType(string? type)
    {
        var visible = new string([.. (type ?? string.Empty).Where(character => character is not ('​' or '‌' or '‍' or '⁠' or '﻿'))]);
        return string.Join(' ', visible.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    ///   Whether a text has letters, and only Latin ones.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns><c>true</c> when it is written in Latin script.</returns>
    private static bool IsLatin(string value)
    {
        var hasLetter = false;
        foreach (var character in value)
        {
            if (!char.IsLetter(character))
                continue;
            if (character > 'ɏ' && character is not (>= 'Ḁ' and <= 'ỿ'))
                return false;

            hasLetter = true;
        }

        return hasLetter;
    }

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
    /// <returns>The title from TMDB, of the listed type.</returns>
    internal static ITitle ToTitle(ListedText text)
        => new TitleStub
        {
            Source = MetadataSource.TMDB,
            Language = Language(text.LanguageCode, text.CountryCode),
            LanguageCode = text.LanguageCode,
            CountryCode = text.CountryCode,
            Value = text.Value,
            Type = text.Type,
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
    /// <param name="Type">The type, for a title: official for a translation, a synonym for an alternative title.</param>
    internal readonly record struct ListedText(string LanguageCode, string CountryCode, string Value, TitleType Type = TitleType.Official);

    #endregion
}
