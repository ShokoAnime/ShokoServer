using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Stub;

namespace Shoko.Server.Services;

/// <summary>
///   Tells a generic episode title, such as <c>Episode 5</c>, <c>Folge 5</c>
///   or <c>第5話</c>, from a real one, and synthesizes one for an episode no
///   source named, a generic name for a season with no title, or a name for
///   any other entry with none.
/// </summary>
/// <remarks>
///   One table holds each language's episode, season and specials forms, the
///   synthesized ones and the others recognised, plus AniDB's <c>Episode S1</c>
///   form for the other episode types and the <c>TBA</c> and <c>TBD</c> of an
///   episode not named yet. Labels such as <c>Opening 2</c>,
///   <c>Special 1</c> or <c>第一章</c> are real titles. A form must fill the
///   whole title and carry the entry's own number to count as its generic one.
/// </remarks>
internal static class GenericEpisodeTitles
{
    #region Forms

    /// <summary>
    ///   The generic forms of each language. The first form of a kind is the
    ///   one synthesized; every form of every language is recognised.
    /// </summary>
    private static readonly FrozenDictionary<TitleLanguage, LanguageForms> _forms = new Dictionary<TitleLanguage, LanguageForms>
    {
        [TitleLanguage.English] = new(["Episode {0}"], "Season {0}", ["Specials"]),
        [TitleLanguage.EnglishAmerican] = new(["Episode {0}"], "Season {0}", ["Specials"]),
        [TitleLanguage.EnglishBritish] = new(["Episode {0}"], "Season {0}", ["Specials"]),
        [TitleLanguage.EnglishAustralian] = new(["Episode {0}"], "Season {0}", ["Specials"]),
        [TitleLanguage.Japanese] = new(["第{0}話", "第{0}回", "エピソード{0}"], "シーズン{0}", ["特別編"]),
        [TitleLanguage.Chinese] = new(["第{0}集", "第{0}话", "第{0}話", "第{0}回", "总第{0}集", "總第{0}集"], "第{0}季", ["特别篇", "特別篇"]),
        [TitleLanguage.ChineseSimplified] = new(["第{0}集", "第{0}话", "第{0}回", "总第{0}集"], "第{0}季", ["特别篇"]),
        [TitleLanguage.ChineseTraditional] = new(["第{0}集", "第{0}話", "第{0}回", "總第{0}集"], "第{0}季", ["特別篇"]),
        // Hangul numerals stay out: 일화 means "anecdote", 이화 and 사화 are real words,
        // and only 2 of 59,093 Korean titles in a real library used them, both likely real.
        [TitleLanguage.Korean] = new(["{0}화", "제{0}화", "에피소드 {0}"], "시즌 {0}", ["스페셜"]),
        [TitleLanguage.French] = new(["Épisode {0}", "Episode {0}"], "Saison {0}", ["Épisodes spéciaux"]),
        [TitleLanguage.FrenchCanadian] = new(["Épisode {0}", "Episode {0}"], "Saison {0}", ["Épisodes spéciaux"]),
        [TitleLanguage.German] = new(["Folge {0}"], "Staffel {0}", ["Specials"]),
        [TitleLanguage.Spanish] = new(["Episodio {0}"], "Temporada {0}", ["Especiales"]),
        [TitleLanguage.Italian] = new(["Episodio {0}"], "Stagione {0}", ["Speciali"]),
        [TitleLanguage.Portuguese] = new(["Episódio {0}", "Episodio {0}"], "Temporada {0}", ["Especiais"]),
        [TitleLanguage.BrazilianPortuguese] = new(["Episódio {0}", "Episodio {0}"], "Temporada {0}", ["Especiais"]),
        [TitleLanguage.Dutch] = new(["Aflevering {0}"], "Seizoen {0}", ["Specials"]),
        [TitleLanguage.Thai] = new(["ตอนที่ {0}", "ตอน {0}"], "ซีซั่น {0}", ["ตอนพิเศษ"]),
        [TitleLanguage.Vietnamese] = new(["Tập {0}"], "Mùa {0}", ["Đặc biệt"]),
        [TitleLanguage.Czech] = new(["{0}. díl"], "{0}. série", ["Speciály"]),
        [TitleLanguage.Croatian] = new(["{0}. epizoda"], "{0}. sezona", ["Specijali"]),
        [TitleLanguage.Hungarian] = new(["{0}. epizód"], "{0}. évad", ["Különkiadások"]),
        [TitleLanguage.Arabic] = new(["حلقة {0}", "الحلقة {0}"], "الموسم {0}", ["حلقات خاصة"]),
        [TitleLanguage.Hindi] = new(["एपिसोड {0}"], "सीज़न {0}", ["विशेष"]),
        [TitleLanguage.Malaysian] = new(["Episod {0}"], "Musim {0}", ["Istimewa"]),
        [TitleLanguage.Indonesian] = new(["Episode {0}"], "Musim {0}", ["Spesial"]),
        [TitleLanguage.Ukrainian] = new(["Епізод {0}"], "Сезон {0}", ["Спецвипуски"]),
        [TitleLanguage.Russian] = new(["Эпизод {0}", "Серия {0}", "{0} эпизод"], "Сезон {0}", ["Спецвыпуски"]),
        [TitleLanguage.Polish] = new(["Odcinek {0}"], "Sezon {0}", ["Odcinki specjalne"]),
        [TitleLanguage.Turkish] = new(["{0}. Bölüm", "Bölüm {0}"], "{0}. Sezon", ["Özel Bölümler"]),
        [TitleLanguage.Hebrew] = new(["פרק {0}"], "עונה {0}", ["ספיישלים"]),
    }.ToFrozenDictionary();

    /// <summary>
    ///   The counter words after which a number may be written in Japanese or
    ///   Chinese numerals: the episode words and the season word. Others,
    ///   such as <c>章</c> or <c>夜</c>, make real titles.
    /// </summary>
    private const string KanjiCounters = "話话集回季";

    /// <summary>
    ///   The Japanese and Chinese numerals, digits and units.
    /// </summary>
    private const string KanjiNumerals = "〇零一二三四五六七八九十百千两兩";

    /// <summary>
    ///   The stand-ins sources give an episode not named yet, in any language:
    ///   <c>TBA</c> and <c>TBD</c>, with or without a period. Others, such as
    ///   <c>To Be Announced</c>, <c>Untitled</c> or <c>未定</c>, are kept.
    /// </summary>
    private const string Placeholders = @"tb[ad]\.?";

    /// <summary>
    ///   The patterns of every language's episode forms, as alternatives.
    /// </summary>
    private static readonly string _episodeForms = string.Join('|', _forms.Values.SelectMany(forms => forms.Episodes).Distinct().Select(Pattern));

    /// <summary>
    ///   Every generic episode form, with the number in the group <c>n</c>
    ///   and AniDB's type letter, if any, in the group <c>t</c>, and the
    ///   stand-ins for an episode not named yet.
    /// </summary>
    private static readonly Regex _episodeRegex = new(
        $@"^(?:{Placeholders}|episode\s*(?<t>(?-i:[SCTPO]))\s*(?<n>\d+)|{_episodeForms})$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled
    );

    /// <summary>
    ///   Every generic season name, numbered or the specials', in every
    ///   language with a form, whatever the number.
    /// </summary>
    private static readonly Regex _seasonNameRegex = new(
        $"^(?:{string.Join('|', _forms.Values.SelectMany(forms => forms.Specials.Prepend(forms.Season)).Distinct().Select(Pattern))})$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled
    );

    /// <summary>
    ///   The generic forms of each language.
    /// </summary>
    internal static IReadOnlyDictionary<TitleLanguage, LanguageForms> Forms => _forms;

    /// <summary>
    ///   The generic forms of one language, with <c>{0}</c> for the number.
    ///   The first form of each kind is the one synthesized.
    /// </summary>
    /// <param name="Episodes">The names of a normal episode.</param>
    /// <param name="Season">The name of a numbered season.</param>
    /// <param name="Specials">The names of the season holding the specials.</param>
    internal sealed record LanguageForms(string[] Episodes, string Season, string[] Specials);

    /// <summary>
    ///   The number an entry's generic titles carry: a season's number, or an
    ///   episode's number with its type and whose forms it takes.
    /// </summary>
    /// <param name="Number">The season's or episode's number.</param>
    /// <param name="Type">The episode's type; a season keeps the default.</param>
    /// <param name="AnidbForms">Whether the episode is AniDB's or the core's, whose other types take AniDB's lettered form.</param>
    internal readonly record struct EntryNumber(int Number, EpisodeType Type = EpisodeType.Episode, bool AnidbForms = false)
    {
        /// <summary>
        ///   The number of an entry, when it is a season or an episode.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <returns>The number, or <c>null</c> for any other entry.</returns>
        internal static EntryNumber? Of(object? entry)
            => entry switch
            {
                ISeason season => new(season.SeasonNumber),
                IEpisode episode => new(episode.EpisodeNumber, episode.Type, episode.ID.Source.IsCore),
                _ => null,
            };
    }

    #endregion

    #region Checking

    /// <summary>
    ///   Whether a title has the shape of a generic episode title, whatever
    ///   its number.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <returns><c>true</c> for a generic title.</returns>
    internal static bool LooksGeneric(string? title)
        => !string.IsNullOrWhiteSpace(title) && _episodeRegex.IsMatch(title.Trim());

    /// <summary>
    ///   Whether a title has the shape of a generic season name, such as
    ///   <c>Season 2</c>, <c>Staffel 2</c> or <c>Specials</c>, whatever its
    ///   number.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <returns><c>true</c> for a generic season name.</returns>
    internal static bool LooksLikeSeasonName(string? title)
        => !string.IsNullOrWhiteSpace(title) && _seasonNameRegex.IsMatch(title.Trim());

    /// <summary>
    ///   Whether a title is a generic one for an entry, which the core
    ///   synthesizes rather than stores: a generic form carrying the entry's
    ///   own number, or an episode's <c>TBA</c> or <c>TBD</c>.
    /// </summary>
    /// <remarks>
    ///   A form with another number, such as <c>Season 11</c> on season 8, is
    ///   a real title, and so is any form when the entry's number is unknown.
    /// </remarks>
    /// <param name="entityType">The entry's kind. Other kinds than episodes and seasons have no generic titles.</param>
    /// <param name="title">The title.</param>
    /// <param name="number">The entry's number, and an episode's type, or <c>null</c> when unknown.</param>
    /// <returns><c>true</c> for a generic title of the entry.</returns>
    internal static bool IsGenericFor(MetadataEntityType entityType, string? title, EntryNumber? number)
    {
        if (entityType == MetadataEntityType.Episode)
            return number is { } episode ? IsGenericForEpisode(title, episode.Type, episode.Number, episode.AnidbForms) : IsPlaceholder(title);

        return entityType == MetadataEntityType.Season && number is { } season && IsGenericForSeason(title, season.Number);
    }

    /// <summary>
    ///   Whether a title is a generic one of one episode, the one synthesized
    ///   for it in any language: its own number in the plain form, AniDB's
    ///   lettered form such as <c>Episode S1</c> for an AniDB or core episode
    ///   of another type, or a <c>TBA</c> or <c>TBD</c>.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="type">The episode's type, which AniDB's lettered form must carry.</param>
    /// <param name="number">The episode's number.</param>
    /// <param name="anidbForms">Whether the episode is AniDB's or the core's, whose other types take AniDB's lettered form.</param>
    /// <returns><c>true</c> for a generic title of the episode.</returns>
    internal static bool IsGenericForEpisode(string? title, EpisodeType type, int number, bool anidbForms)
    {
        if (string.IsNullOrWhiteSpace(title) || _episodeRegex.Match(title.Trim()) is not { Success: true } match)
            return false;

        // A stand-in has no number.
        if (!match.Groups["n"].Success)
            return true;
        if (ParseNumber(match.Groups["n"].Value) != number)
            return false;

        // The lettered form, or the plain one, as the episode's title is synthesized.
        var lettered = anidbForms && type is not EpisodeType.Episode;
        return match.Groups["t"].Success
            ? lettered && Enum.IsDefined(type) && match.Groups["t"].Value == type.Prefix
            : !lettered;
    }

    /// <summary>
    ///   Whether a title is a stand-in for an episode not named yet,
    ///   <c>TBA</c> or <c>TBD</c>, which is generic on any episode.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <returns><c>true</c> for a stand-in.</returns>
    private static bool IsPlaceholder(string? title)
        => !string.IsNullOrWhiteSpace(title) && _episodeRegex.Match(title.Trim()) is { Success: true } match && !match.Groups["n"].Success;

    /// <summary>
    ///   Whether a title is the generic title of one episode: its own type
    ///   and number, in any of the known forms.
    /// </summary>
    /// <remarks>
    ///   A look-alike with another number, such as <c>Episode 17</c> on
    ///   episode 1, is not the episode's generic title.
    /// </remarks>
    /// <param name="title">The title.</param>
    /// <param name="type">The episode's type.</param>
    /// <param name="number">The episode's number.</param>
    /// <returns><c>true</c> when the title is the episode's generic title.</returns>
    internal static bool IsGeneric(string? title, EpisodeType type, int number)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        if (IsEnglishGeneric(title, type, number))
            return true;

        // The prefixed English form was checked above, and every other form
        // names a normal episode.
        var match = _episodeRegex.Match(title.Trim());
        return type is EpisodeType.Episode && match.Success && !match.Groups["t"].Success && ParseNumber(match.Groups["n"].Value) == number;
    }

    /// <summary>
    ///   Whether a title is AniDB's English generic title of one episode,
    ///   <c>Episode {prefix}{number}</c>, such as <c>Episode 5</c> or
    ///   <c>Episode S1</c>.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="type">The episode's type, which gives the prefix.</param>
    /// <param name="number">The episode's number.</param>
    /// <returns><c>true</c> when the title is exactly that form, in any case.</returns>
    internal static bool IsEnglishGeneric(string? title, EpisodeType type, int number)
        => !string.IsNullOrWhiteSpace(title) && title.Trim().Equals($"Episode {type.Prefix}{number}", StringComparison.InvariantCultureIgnoreCase);

    /// <summary>
    ///   Whether a title is a generic English name of one season,
    ///   <c>Season {number}</c>, or also <c>Specials</c> for season 0.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="number">The season's number.</param>
    /// <returns><c>true</c> when the title is that name, in any case.</returns>
    internal static bool IsGenericSeasonName(string? title, int number)
        => !string.IsNullOrWhiteSpace(title) && (
            title.Trim().Equals(SeasonName(number), StringComparison.InvariantCultureIgnoreCase) ||
            title.Trim().Equals(string.Create(CultureInfo.InvariantCulture, $"Season {number}"), StringComparison.InvariantCultureIgnoreCase)
        );

    /// <summary>
    ///   Whether a name is the generic name of one season in any language:
    ///   its own number in the season form, such as <c>Staffel 2</c> or
    ///   <c>第2季</c> on season 2, or a specials form on season 0.
    /// </summary>
    /// <param name="title">The name.</param>
    /// <param name="number">The season's number; <c>0</c> for the specials.</param>
    /// <returns><c>true</c> when the name is the season's generic name.</returns>
    internal static bool IsGenericForSeason(string? title, int number)
    {
        if (string.IsNullOrWhiteSpace(title) || _seasonNameRegex.Match(title.Trim()) is not { Success: true } match)
            return false;

        // Only the season form has a number; the specials forms have none.
        return match.Groups["n"].Success ? ParseNumber(match.Groups["n"].Value) == number : number is 0;
    }

    /// <summary>
    ///   The generic English name of one season.
    /// </summary>
    /// <param name="number">The season's number.</param>
    /// <returns><c>Season {number}</c>, or <c>Specials</c> for season 0.</returns>
    internal static string SeasonName(int number)
        => number is 0 ? "Specials" : string.Create(CultureInfo.InvariantCulture, $"Season {number}");

    /// <summary>
    ///   The pattern matching a form with any number, with leading zeros or
    ///   in full-width digits, and any spacing around it and between words.
    ///   Japanese and Chinese numerals count only before a counter word.
    /// </summary>
    /// <param name="form">The form, with <c>{0}</c> for the number, if any.</param>
    /// <returns>The pattern, unanchored, with the number in the group <c>n</c>.</returns>
    private static string Pattern(string form)
    {
        if (form.Split("{0}") is not [var before, var after])
            return Words(form);

        var number = after.TrimStart() is [var counter, ..] && KanjiCounters.Contains(counter) ? $@"\d+|[{KanjiNumerals}]+" : @"\d+";
        return $@"{Words(before)}\s*(?<n>{number})\s*{Words(after)}";
    }

    /// <summary>
    ///   The pattern matching some words with any spacing between them.
    /// </summary>
    /// <param name="text">The words.</param>
    /// <returns>The pattern.</returns>
    private static string Words(string text)
        => string.Join(@"\s*", text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape));

    /// <summary>
    ///   Reads a number in any decimal digits, or in Japanese or Chinese
    ///   numerals, such as <c>二十五</c> or <c>一百零二</c>.
    /// </summary>
    /// <param name="text">The number, as the forms match it.</param>
    /// <returns>The number, or <c>null</c> when it is empty or too large.</returns>
    private static int? ParseNumber(string text)
    {
        // Without a unit, numerals are read digit by digit, as in 二〇.
        var positional = !text.Any(character => character is '十' or '百' or '千');
        long total = 0, digit = 0;
        foreach (var character in text)
        {
            var unit = character switch { '十' => 10, '百' => 100, '千' => 1000, _ => 0 };
            if (unit is not 0)
            {
                total += (digit is 0 ? 1 : digit) * unit;
                digit = 0;
            }
            else if (positional)
            {
                total = total * 10 + Digit(character);
            }
            else
            {
                digit = Digit(character);
            }

            if (total > int.MaxValue)
                return null;
        }

        return text.Length is 0 || total + digit > int.MaxValue ? null : (int)(total + digit);
    }

    /// <summary>
    ///   The value of one digit, decimal or a Japanese or Chinese numeral.
    /// </summary>
    /// <param name="character">The digit.</param>
    /// <returns>The value, <c>0</c> for anything else.</returns>
    private static int Digit(char character)
        => char.IsDigit(character) ? (int)char.GetNumericValue(character)
            : character is '两' or '兩' ? 2
            : Math.Max(0, "〇一二三四五六七八九".IndexOf(character));

    #endregion

    #region Synthesizing

    /// <summary>
    ///   Synthesizes a generic title for an episode, in the first of the given
    ///   languages that has a form for it, else in English.
    /// </summary>
    /// <param name="type">The episode's type.</param>
    /// <param name="number">The episode's number.</param>
    /// <param name="anidbForms">Whether the episode is AniDB's or the core's, whose other types take AniDB's forms.</param>
    /// <param name="languages">The languages to try, best first.</param>
    /// <param name="titleType">The type the title is listed with.</param>
    /// <param name="source">Optional. The source the title is listed under, <c>generated</c> when left out.</param>
    /// <returns>The title, marked as synthesized.</returns>
    internal static ITitle Synthesize(
        EpisodeType type,
        int number,
        bool anidbForms,
        IEnumerable<TitleLanguage> languages,
        TitleType titleType = TitleType.None,
        MetadataSource? source = null
    )
        => SynthesizeAll(type, number, anidbForms, languages, titleType, source)[0];

    /// <summary>
    ///   Synthesizes a generic title for an episode in each of the given
    ///   languages that has a form for it, then in English.
    /// </summary>
    /// <remarks>
    ///   An AniDB or core episode of another type than a normal one gets only
    ///   AniDB's English form, such as <c>Episode S3</c> or <c>Episode C1</c>.
    ///   Every other episode, another source's specials included, gets the
    ///   plain episode form, such as <c>Episode 3</c>.
    /// </remarks>
    /// <param name="type">The episode's type.</param>
    /// <param name="number">The episode's number.</param>
    /// <param name="anidbForms">Whether the episode is AniDB's or the core's, whose other types take AniDB's forms.</param>
    /// <param name="languages">The languages to try, best first.</param>
    /// <param name="firstType">The type the first title is listed with; the others have none.</param>
    /// <param name="source">Optional. The source the titles are listed under, <c>generated</c> when left out.</param>
    /// <returns>The titles, each value once, marked as synthesized.</returns>
    internal static IReadOnlyList<ITitle> SynthesizeAll(
        EpisodeType type,
        int number,
        bool anidbForms,
        IEnumerable<TitleLanguage> languages,
        TitleType firstType = TitleType.Main,
        MetadataSource? source = null
    )
        => SynthesizeEach(languages, firstType, source ?? MetadataSource.Generated, language => anidbForms && type is not EpisodeType.Episode
            ? language is TitleLanguage.English ? $"Episode {type.Prefix}{number}" : null
            : _forms.TryGetValue(language, out var forms) ? string.Format(CultureInfo.InvariantCulture, forms.Episodes[0], number) : null);

    /// <summary>
    ///   Synthesizes the generic name of a season in each of the given languages
    ///   that has a form for it, then in English.
    /// </summary>
    /// <param name="number">The season's number; <c>0</c> is named as the specials.</param>
    /// <param name="languages">The languages to try, best first.</param>
    /// <param name="firstType">The type the first title is listed with; the others have none.</param>
    /// <returns>The titles, each value once, marked as synthesized.</returns>
    internal static IReadOnlyList<ITitle> SynthesizeSeasonAll(int number, IEnumerable<TitleLanguage> languages, TitleType firstType = TitleType.Main)
        => SynthesizeEach(languages, firstType, MetadataSource.Generated, language => _forms.TryGetValue(language, out var forms)
            ? number is 0 ? forms.Specials[0] : string.Format(CultureInfo.InvariantCulture, forms.Season, number)
            : null);

    /// <summary>
    ///   Synthesizes the name of an entry with no generic name, such as a
    ///   series: its source, kind and ID, as in <c>TMDb Series 46195</c>.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <returns>The title, in no known language, listed as the main one and marked as synthesized.</returns>
    internal static ITitle SynthesizeName(MetadataGuid entityID)
        => new SynthesizedTitle
        {
            Source = MetadataSource.Generated,
            Language = TitleLanguage.Unknown,
            LanguageCode = "unk",
            Value = $"{entityID.Source.Name} {entityID.EntityType.Name} {entityID.ID}",
            Type = TitleType.Main,
        };

    /// <summary>
    ///   Synthesizes a title in each of the given languages that has a form,
    ///   then in English, leaving out a value already synthesized.
    /// </summary>
    /// <param name="languages">The languages to try, best first.</param>
    /// <param name="firstType">The type the first title is listed with; the others have none.</param>
    /// <param name="source">The source the titles are listed under.</param>
    /// <param name="name">The value in a language, or <c>null</c> when it has no form.</param>
    /// <returns>The titles, at least the English one, marked as synthesized.</returns>
    private static List<ITitle> SynthesizeEach(
        IEnumerable<TitleLanguage> languages,
        TitleType firstType,
        MetadataSource source,
        Func<TitleLanguage, string?> name
    )
    {
        var titles = new List<ITitle>();
        foreach (var language in languages.Append(TitleLanguage.English))
        {
            if (name(language) is not { } value || titles.Any(title => title.Value == value))
                continue;

            var (languageCode, countryCode) = language.GetLanguageAndCountryCode();
            titles.Add(new SynthesizedTitle
            {
                Source = source,
                Language = language,
                LanguageCode = languageCode,
                CountryCode = countryCode,
                Value = value,
                Type = titles.Count is 0 ? firstType : TitleType.None,
            });
        }

        return titles;
    }

    /// <summary>
    ///   A title synthesized on the spot.
    /// </summary>
    private sealed class SynthesizedTitle : TitleStub, ITitle
    {
        bool ITitle.IsSynthesized => true;
    }

    #endregion
}
