using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Stub;
using TMDbLib.Objects.General;

namespace Shoko.Plugin.Tmdb.Mapping;

/// <summary>
///   Works out which of the titles and overviews TMDb lists for an entry
///   are stored, and in what order.
/// </summary>
/// <remarks>
///   The American English text comes first, the title as the entry's main
///   one, then the original title, then the translations in TMDb's order,
///   then the transcriptions among the alternative titles. A translation
///   equal to the English or the original title is not stored again, nor is
///   a generic episode title with the episode's own number, nor an English
///   generic season name.
/// </remarks>
public static partial class TmdbTexts
{
    #region Titles

    /// <summary>
    ///   The titles to store for an entry.
    /// </summary>
    /// <param name="englishTitle">The American English title, which TMDb falls back to the original for.</param>
    /// <param name="originalTitle">The title in the original language, if TMDb has one.</param>
    /// <param name="originalLanguageCode">The original language's code, if known.</param>
    /// <param name="translations">The translations TMDb lists, or <see langword="null"/>.</param>
    /// <param name="alternativeTitles">The alternative titles TMDb lists, for a show or a movie, or <see langword="null"/>.</param>
    /// <param name="languages">The languages to keep translations in besides the English and original ones, or <see langword="null"/> for all.</param>
    /// <param name="episodeNumber">The episode's number, for an episode's titles, or <see langword="null"/>.</param>
    /// <param name="ownName">
    ///   The name TMDb gives the entry itself, which, without an English title, marks the main one among the others, or is the main one
    ///   in no known language.
    /// </param>
    /// <param name="seasonNumber">The season's number, for a season's titles, or <see langword="null"/>.</param>
    /// <returns>The titles, in order.</returns>
    public static IReadOnlyList<ITitle> Titles(
        string? englishTitle,
        string? originalTitle,
        string? originalLanguageCode,
        TranslationsContainer? translations,
        IEnumerable<AlternativeTitle>? alternativeTitles,
        IReadOnlySet<TitleLanguage>? languages,
        int? episodeNumber = null,
        string? ownName = null,
        int? seasonNumber = null
    )
    {
        var original = originalLanguageCode?.Trim().ToLowerInvariant();
        var english = Clean(englishTitle);
        var originalValue = Clean(originalTitle);
        var titles = new List<ITitle>();
        var seen = new HashSet<(string, string, TitleType)>();

        void Add(string languageCode, string countryCode, string? value, TitleType type)
        {
            if (Clean(value) is not { } text || (episodeNumber is { } number && IsGenericEpisodeTitle(text, number)))
                return;
            if (seasonNumber is { } season && languageCode is "en" && IsGenericSeasonName(text, season))
                return;
            if (!seen.Add((languageCode, countryCode, type is TitleType.Synonym ? TitleType.Synonym : TitleType.Official)))
                return;

            titles.Add(Title(languageCode, countryCode, text, type));
        }

        // Without an English title, the original title, or the one TMDb names the entry by, is the main one.
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
    /// <param name="englishOverview">The American English overview, if TMDb has one.</param>
    /// <param name="translations">The translations TMDb lists, or <see langword="null"/>.</param>
    /// <param name="languages">The languages to keep besides American English, or <see langword="null"/> for all.</param>
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
    ///   The English text of an entry: the one TMDb's English translation
    ///   gives, or the entry's own when it gives none and it is English.
    /// </summary>
    /// <remarks>
    ///   Without an English translation TMDb gives the original language's
    ///   text as the entry's own. That counts as English only for an entry
    ///   first made in English, or when it is in the Latin script and is not
    ///   the original title, a translation's text or a generic season name.
    ///   A season's generic name is never English text, even when listed.
    /// </remarks>
    /// <param name="translations">The translations TMDb lists, or <see langword="null"/>.</param>
    /// <param name="read">Reads the text out of a translation.</param>
    /// <param name="fallback">The entry's own text.</param>
    /// <param name="originalTitle">The title in the original language, for a show's or movie's title, or <see langword="null"/>.</param>
    /// <param name="originalLanguageCode">The original language's code, if known.</param>
    /// <param name="seasonNumber">The season's number, for a season's title, or <see langword="null"/>.</param>
    /// <returns>The text, or <see langword="null"/> when there is none.</returns>
    public static string? English(
        TranslationsContainer? translations,
        Func<TranslationData, string?> read,
        string? fallback,
        string? originalTitle = null,
        string? originalLanguageCode = null,
        int? seasonNumber = null
    )
    {
        var translation = translations?.Translations?.FirstOrDefault(translation => translation.Iso_639_1 is "en" && translation.Iso_3166_1 is "US")
            ?? translations?.Translations?.FirstOrDefault(translation => translation.Iso_639_1 is "en");
        if (translation?.Data is { } data && Clean(read(data)) is { } text && !(seasonNumber is { } season && IsGenericSeasonName(text, season)))
            return text;

        if (Clean(fallback) is not { } value)
            return null;
        if (originalLanguageCode?.Trim().ToLowerInvariant() is "en")
            return value;
        if (!IsLatinScript(value) || string.Equals(value, Clean(originalTitle), StringComparison.Ordinal) ||
            (seasonNumber is { } number && IsGenericSeasonName(value, number)) ||
            translations?.Translations?.Any(other => other.Data is { } otherData && string.Equals(Clean(read(otherData)), value, StringComparison.Ordinal)) is true)
            return null;

        return value;
    }

    /// <summary>
    ///   Whether a title is a generic English name of one season,
    ///   <c>Season 5</c>, or also <c>Specials</c> for season 0, which the
    ///   core makes up itself and is never stored.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="seasonNumber">The season's number.</param>
    /// <returns><see langword="true"/> for the season's generic name.</returns>
    public static bool IsGenericSeasonName(string title, int seasonNumber)
        => title.Trim().Equals($"Season {seasonNumber}", StringComparison.InvariantCultureIgnoreCase) ||
            (seasonNumber is 0 && title.Trim().Equals("Specials", StringComparison.InvariantCultureIgnoreCase));

    #endregion

    #region Languages

    /// <summary>
    ///   The languages to keep translations in, from the order the settings
    ///   give, or <see langword="null"/> to keep them all.
    /// </summary>
    /// <param name="downloadAll">Whether every translation is kept.</param>
    /// <param name="order">The languages the settings rank.</param>
    /// <returns>The languages.</returns>
    public static IReadOnlySet<TitleLanguage>? KeptLanguages(bool downloadAll, IEnumerable<TitleLanguage> order)
        => downloadAll ? null : order.Where(language => language is not (TitleLanguage.Main or TitleLanguage.Unknown or TitleLanguage.None)).ToHashSet();

    /// <summary>
    ///   The language of a TMDb text, from its codes. TMDb's own <c>xx</c>
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

    #region Episode Titles

    /// <summary>
    ///   The forms TMDb gives an episode with no title of its own, in the
    ///   languages it translates them to, with the number in the group
    ///   <c>n</c>.
    /// </summary>
    /// <returns>The expression.</returns>
    [GeneratedRegex(
        """
        ^(?:
          episode\s*(?<n>\d+)
        | [ée]pisode\s*(?<n>\d+)
        | episodio\s*(?<n>\d+)
        | epis[óo]dio\s*(?<n>\d+)
        | folge\s*(?<n>\d+)
        | aflevering\s*(?<n>\d+)
        | 제?\s*(?<n>\d+)\s*화
        | 第\s*(?<n>\d+)\s*[集话話]
        | ตอน(?:ที่)?\s*(?<n>\d+)
        | tập\s*(?<n>\d+)
        | (?<n>\d+)\.\s*(?:díl|epizoda|epizód|bölüm)
        | (?:ال)?حلقة\s*(?<n>\d+)
        | एपिसोड\s*(?<n>\d+)
        | episod\s*(?<n>\d+)
        | епізод\s*(?<n>\d+)
        | эпизод\s*(?<n>\d+)
        | серия\s*(?<n>\d+)
        | odcinek\s*(?<n>\d+)
        | פרק\s*(?<n>\d+)
        )$
        """,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace
    )]
    private static partial Regex GenericEpisodeTitleRegex();

    /// <summary>
    ///   Whether a title is the generic title of an episode with this number,
    ///   such as <c>Episode 5</c> or <c>第5話</c>, which the core makes up
    ///   itself and is never stored.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="episodeNumber">The episode's number.</param>
    /// <returns><see langword="true"/> for a generic title of this episode.</returns>
    public static bool IsGenericEpisodeTitle(string title, int episodeNumber)
        => GenericEpisodeTitleRegex().Match(title.Trim()) is { Success: true } match &&
            int.TryParse(match.Groups["n"].ValueSpan, out var number) && number == episodeNumber;

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
    ///   The transcriptions among the alternative titles TMDb lists, at most
    ///   one per transcription language.
    /// </summary>
    /// <remarks>
    ///   A type naming its system decides the language; a generic type takes
    ///   the original language's, in that language's home countries only.
    ///   Only Latin text counts. Home-country and plain ASCII titles go first.
    /// </remarks>
    /// <param name="alternativeTitles">The alternative titles, or <see langword="null"/>.</param>
    /// <param name="originalLanguageCode">The original language's code, or <see langword="null"/>.</param>
    /// <returns>The transcriptions' language codes and values, in TMDb's order.</returns>
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
    ///   A title from TMDb.
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
    ///   An overview from TMDb.
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
    ///   A text trimmed, or <see langword="null"/> when it is blank.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The trimmed text, or <see langword="null"/>.</returns>
    public static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    #endregion
}
