using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Stub;
using TMDbLib.Objects.General;

namespace Shoko.Plugin.Tmdb.Mapping;

/// <summary>
///   Works out which of the titles and overviews TMDB lists for an entry
///   are stored, and in what order.
/// </summary>
/// <remarks>
///   The American English text comes first, the title as the entry's main
///   one, then the original title, then the translations in TMDB's order,
///   then the transcriptions among the alternative titles. A translation
///   equal to the English or the original title is not stored again.
///   Generic titles are passed on as TMDB gives them; the core drops them.
/// </remarks>
public static class TmdbTexts
{
    #region Titles

    /// <summary>
    ///   The titles to store for an entry.
    /// </summary>
    /// <param name="englishTitle">The American English title, which TMDB falls back to the original for.</param>
    /// <param name="originalTitle">The title in the original language, if TMDB has one.</param>
    /// <param name="originalLanguageCode">The original language's code, if known.</param>
    /// <param name="translations">The translations TMDB lists, or <c>null</c>.</param>
    /// <param name="alternativeTitles">The alternative titles TMDB lists, for a show or a movie, or <c>null</c>.</param>
    /// <param name="languages">The languages to keep translations in besides the English and original ones, or <c>null</c> for all.</param>
    /// <param name="ownName">
    ///   The name TMDB gives the entry itself, which, without an English title, marks the main one among the others, or is the main one
    ///   in no known language.
    /// </param>
    /// <returns>The titles, in order.</returns>
    public static IReadOnlyList<ITitle> Titles(
        string? englishTitle,
        string? originalTitle,
        string? originalLanguageCode,
        TranslationsContainer? translations,
        IEnumerable<AlternativeTitle>? alternativeTitles,
        IReadOnlySet<TitleLanguage>? languages,
        string? ownName = null
    )
    {
        var original = originalLanguageCode?.Trim().ToLowerInvariant();
        var english = Clean(englishTitle);
        var originalValue = Clean(originalTitle);
        var titles = new List<ITitle>();
        var seen = new HashSet<(string, string, TitleType)>();

        void Add(string languageCode, string countryCode, string? value, TitleType type)
        {
            if (Clean(value) is not { } text)
                return;
            if (!seen.Add((languageCode, countryCode, type is TitleType.Synonym ? TitleType.Synonym : TitleType.Official)))
                return;

            titles.Add(Title(languageCode, countryCode, text, type));
        }

        // Without an English title, the original title, or the one TMDB names the entry by, is the main one.
        Add("en", "US", english, TitleType.Main);
        if (!string.IsNullOrEmpty(original) && original is not "en")
            Add(original, string.Empty, originalValue, english is null ? TitleType.Main : TitleType.Official);

        foreach (var translation in translations?.Translations ?? [])
        {
            var languageCode = translation.Iso_639_1?.Trim().ToLowerInvariant() ?? string.Empty;
            var countryCode = translation.Iso_3166_1?.Trim().ToUpperInvariant() ?? string.Empty;
            if (languageCode.Length is 0 || (languageCode is "en" && countryCode is "US"))
                continue;

            var value = Clean(translation.Data?.Name);
            if (value is null)
                continue;

            if (languageCode == original)
            {
                // The original title already stands for its language.
                if (originalValue is not null)
                    continue;
            }
            else if (!Wanted(languages, languageCode, countryCode) ||
                string.Equals(value, english, StringComparison.InvariantCultureIgnoreCase) ||
                string.Equals(value, originalValue, StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }

            Add(languageCode, countryCode, value, TitleType.Official);
        }

        foreach (var transcribed in TranscribedTitles(alternativeTitles, original))
        {
            if (Wanted(languages, transcribed.LanguageCode, null))
                Add(transcribed.LanguageCode, string.Empty, transcribed.Value, TitleType.Synonym);
        }

        if (english is null && Clean(ownName) is { } name && !titles.Any(title => title.Type is TitleType.Main))
        {
            if (titles.FindIndex(title => title.Type is TitleType.Official && title.Value == name) is >= 0 and var index)
                titles[index] = Title(titles[index].LanguageCode, titles[index].CountryCode ?? string.Empty, name, TitleType.Main);
            else
                titles.Add(Title("unk", string.Empty, name, TitleType.Main));
        }

        return titles;
    }

    #endregion

    #region Overviews

    /// <summary>
    ///   The overviews to store for an entry.
    /// </summary>
    /// <param name="englishOverview">The American English overview, if TMDB has one.</param>
    /// <param name="translations">The translations TMDB lists, or <c>null</c>.</param>
    /// <param name="languages">The languages to keep besides American English, or <c>null</c> for all.</param>
    /// <returns>The overviews, in order.</returns>
    public static IReadOnlyList<IText> Overviews(string? englishOverview, TranslationsContainer? translations, IReadOnlySet<TitleLanguage>? languages)
    {
        var overviews = new List<IText>();
        var seen = new HashSet<(string, string)>();
        if (Clean(englishOverview) is { } english)
        {
            overviews.Add(Overview("en", "US", english));
            seen.Add(("en", "US"));
        }

        foreach (var translation in translations?.Translations ?? [])
        {
            var languageCode = translation.Iso_639_1?.Trim().ToLowerInvariant() ?? string.Empty;
            var countryCode = translation.Iso_3166_1?.Trim().ToUpperInvariant() ?? string.Empty;
            if (languageCode.Length is 0 || !Wanted(languages, languageCode, countryCode) || Clean(translation.Data?.Overview) is not { } value)
                continue;
            if (seen.Add((languageCode, countryCode)))
                overviews.Add(Overview(languageCode, countryCode, value));
        }

        return overviews;
    }

    /// <summary>
    ///   The English text of an entry: the one TMDB's English translation
    ///   gives, or the entry's own when it gives none and it is English.
    /// </summary>
    /// <remarks>
    ///   Without an English translation TMDB gives the original language's
    ///   text as the entry's own. That counts as English only for an entry
    ///   first made in English, or when it is in the Latin script and is not
    ///   the original title or a translation's text.
    /// </remarks>
    /// <param name="translations">The translations TMDB lists, or <c>null</c>.</param>
    /// <param name="read">Reads the text out of a translation.</param>
    /// <param name="fallback">The entry's own text.</param>
    /// <param name="originalTitle">The title in the original language, for a show's or movie's title, or <c>null</c>.</param>
    /// <param name="originalLanguageCode">The original language's code, if known.</param>
    /// <returns>The text, or <c>null</c> when there is none.</returns>
    public static string? English(
        TranslationsContainer? translations,
        Func<TranslationData, string?> read,
        string? fallback,
        string? originalTitle = null,
        string? originalLanguageCode = null
    )
    {
        var translation = translations?.Translations?.FirstOrDefault(translation => translation.Iso_639_1 is "en" && translation.Iso_3166_1 is "US")
            ?? translations?.Translations?.FirstOrDefault(translation => translation.Iso_639_1 is "en");
        if (translation?.Data is { } data && Clean(read(data)) is { } text)
            return text;

        if (Clean(fallback) is not { } value)
            return null;
        if (originalLanguageCode?.Trim().ToLowerInvariant() is "en")
            return value;
        if (!IsLatinScript(value) || string.Equals(value, Clean(originalTitle), StringComparison.Ordinal) ||
            translations?.Translations?.Any(other => other.Data is { } otherData && string.Equals(Clean(read(otherData)), value, StringComparison.Ordinal)) is true)
            return null;

        return value;
    }

    #endregion

    #region Languages

    /// <summary>
    ///   The languages to keep translations in, from the order the settings
    ///   give, or <c>null</c> to keep them all.
    /// </summary>
    /// <param name="downloadAll">Whether every translation is kept.</param>
    /// <param name="order">The languages the settings rank.</param>
    /// <returns>The languages.</returns>
    public static IReadOnlySet<TitleLanguage>? KeptLanguages(bool downloadAll, IEnumerable<TitleLanguage> order)
        => downloadAll ? null : order.Where(language => language is not (TitleLanguage.Main or TitleLanguage.Unknown or TitleLanguage.None)).ToHashSet();

    /// <summary>
    ///   The language of a TMDB text, from its codes. TMDB's own <c>xx</c>
    ///   means no language and <c>cn</c> Cantonese; a code no language
    ///   matches is unknown, without reporting it.
    /// </summary>
    /// <param name="languageCode">The language code.</param>
    /// <param name="countryCode">The country code, which may be empty.</param>
    /// <returns>The language.</returns>
    public static TitleLanguage Language(string? languageCode, string? countryCode)
    {
        var code = languageCode?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(code) || code is "xx")
            return TitleLanguage.None;
        if (code is "cn")
            return TitleLanguage.Chinese;
        if (!string.IsNullOrWhiteSpace(countryCode) && $"{code}-{countryCode.Trim()}".TryGetTitleLanguage(out var regional))
            return regional;
        return code.TryGetTitleLanguage(out var language) ? language : TitleLanguage.Unknown;
    }

    private static bool Wanted(IReadOnlySet<TitleLanguage>? languages, string languageCode, string? countryCode)
        => languages is null ||
            languages.Contains(Language(languageCode, null)) ||
            (!string.IsNullOrEmpty(countryCode) && languages.Contains(Language(languageCode, countryCode)));

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
    ///   The transcriptions among the alternative titles TMDB lists, at most
    ///   one per transcription language.
    /// </summary>
    /// <remarks>
    ///   A type naming its system decides the language; a generic type takes
    ///   the original language's, in that language's home countries only.
    ///   Only Latin text counts. Home-country and plain ASCII titles go first.
    /// </remarks>
    /// <param name="alternativeTitles">The alternative titles, or <c>null</c>.</param>
    /// <param name="originalLanguageCode">The original language's code, or <c>null</c>.</param>
    /// <returns>The transcriptions' language codes and values, in TMDB's order.</returns>
    public static IReadOnlyList<(string LanguageCode, string Value)> TranscribedTitles(IEnumerable<AlternativeTitle>? alternativeTitles, string? originalLanguageCode)
    {
        var fallback = originalLanguageCode?.Trim().ToLowerInvariant() switch
        {
            "ja" => TitleLanguage.Romaji,
            "zh" or "cn" => TitleLanguage.Pinyin,
            "ko" => TitleLanguage.KoreanTranscription,
            _ => (TitleLanguage?)null,
        };
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
                .Select(candidate => (candidate.Language.GetString(), candidate.Value)),
        ];
    }

    private static bool IsHomeCountry(TitleLanguage language, string? countryCode)
        => language switch
        {
            TitleLanguage.Romaji => countryCode is "JP",
            TitleLanguage.Pinyin => countryCode is "CN" or "TW" or "HK" or "MO" or "SG",
            TitleLanguage.KoreanTranscription => countryCode is "KR",
            _ => false,
        };

    private static string NormalizeType(string? type)
    {
        var visible = new string([.. (type ?? string.Empty).Where(character => character is not ('​' or '‌' or '‍' or '⁠' or '﻿'))]);
        return string.Join(' ', visible.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool IsLatinScript(string value)
        => value.All(character => !char.IsLetter(character) || character <= 'ɏ' || character is >= 'Ḁ' and <= 'ỿ');

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

    #region Helpers

    /// <summary>
    ///   A title from TMDB.
    /// </summary>
    /// <param name="languageCode">The language code, in lower case.</param>
    /// <param name="countryCode">The country code, in upper case, or empty.</param>
    /// <param name="value">The title.</param>
    /// <param name="type">What kind of title it is.</param>
    /// <returns>The title.</returns>
    public static ITitle Title(string languageCode, string countryCode, string value, TitleType type)
        => new TitleStub
        {
            Source = MetadataSource.TMDB,
            Language = Language(languageCode, countryCode),
            LanguageCode = languageCode,
            CountryCode = countryCode.Length is 0 ? null : countryCode,
            Value = value,
            Type = type,
        };

    /// <summary>
    ///   An overview from TMDB.
    /// </summary>
    /// <param name="languageCode">The language code, in lower case.</param>
    /// <param name="countryCode">The country code, in upper case, or empty.</param>
    /// <param name="value">The overview.</param>
    /// <returns>The overview.</returns>
    public static IText Overview(string languageCode, string countryCode, string value)
        => new TextStub
        {
            Source = MetadataSource.TMDB,
            Language = Language(languageCode, countryCode),
            LanguageCode = languageCode,
            CountryCode = countryCode.Length is 0 ? null : countryCode,
            Value = value,
        };

    /// <summary>
    ///   A text trimmed, or <c>null</c> when it is blank.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The trimmed text, or <c>null</c>.</returns>
    public static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    #endregion
}
