using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Stub;

namespace Shoko.Server.Services;

/// <summary>
///   Tells a generic episode title, such as <c>Episode 5</c>, <c>Folge 5</c>
///   or <c>第5話</c>, from a real one, and makes one up for an episode no
///   source named.
/// </summary>
/// <remarks>
///   The forms are the ones AniDB and TMDB use for normal episodes, in about
///   thirty languages, and AniDB's <c>Episode S1</c> form for the other
///   episode types. Numbered labels such as <c>Opening 2</c> or
///   <c>Special 1</c> are real titles. Every form must fill the whole title,
///   so a real title that merely starts with a number never counts.
/// </remarks>
internal static partial class GenericEpisodeTitles
{
    #region Forms

    /// <summary>
    ///   The English word for each episode type, in made-up titles.
    /// </summary>
    private static readonly FrozenDictionary<EpisodeType, string> _typeWords = new Dictionary<EpisodeType, string>
    {
        [EpisodeType.Episode] = "Episode",
        [EpisodeType.Special] = "Special",
        [EpisodeType.Credits] = "Credits",
        [EpisodeType.Trailer] = "Trailer",
        [EpisodeType.Parody] = "Parody",
        [EpisodeType.Other] = "Other",
    }.ToFrozenDictionary();

    /// <summary>
    ///   How each language writes a normal episode's generic title.
    /// </summary>
    private static readonly FrozenDictionary<TitleLanguage, string> _normalForms = new Dictionary<TitleLanguage, string>
    {
        [TitleLanguage.English] = "Episode {0}",
        [TitleLanguage.EnglishAmerican] = "Episode {0}",
        [TitleLanguage.EnglishBritish] = "Episode {0}",
        [TitleLanguage.EnglishAustralian] = "Episode {0}",
        [TitleLanguage.Japanese] = "第{0}話",
        [TitleLanguage.Chinese] = "第{0}集",
        [TitleLanguage.ChineseSimplified] = "第{0}集",
        [TitleLanguage.ChineseTraditional] = "第{0}集",
        [TitleLanguage.Korean] = "{0}화",
        [TitleLanguage.French] = "Épisode {0}",
        [TitleLanguage.FrenchCanadian] = "Épisode {0}",
        [TitleLanguage.German] = "Folge {0}",
        [TitleLanguage.Spanish] = "Episodio {0}",
        [TitleLanguage.Italian] = "Episodio {0}",
        [TitleLanguage.Portuguese] = "Episódio {0}",
        [TitleLanguage.BrazilianPortuguese] = "Episódio {0}",
        [TitleLanguage.Dutch] = "Aflevering {0}",
        [TitleLanguage.Thai] = "ตอนที่ {0}",
        [TitleLanguage.Vietnamese] = "Tập {0}",
        [TitleLanguage.Czech] = "{0}. díl",
        [TitleLanguage.Croatian] = "{0}. epizoda",
        [TitleLanguage.Hungarian] = "{0}. epizód",
        [TitleLanguage.Arabic] = "حلقة {0}",
        [TitleLanguage.Hindi] = "एपिसोड {0}",
        [TitleLanguage.Malaysian] = "Episod {0}",
        [TitleLanguage.Indonesian] = "Episode {0}",
        [TitleLanguage.Ukrainian] = "Епізод {0}",
        [TitleLanguage.Russian] = "Эпизод {0}",
        [TitleLanguage.Polish] = "Odcinek {0}",
        [TitleLanguage.Turkish] = "{0}. Bölüm",
        [TitleLanguage.Hebrew] = "פרק {0}",
    }.ToFrozenDictionary();

    /// <summary>
    ///   Every generic form, with the number in the group <c>n</c> and
    ///   AniDB's type letter, if any, in the group <c>t</c>.
    /// </summary>
    [GeneratedRegex(
        """
        ^(?:
          episode\s*(?<t>[SCTPO])?\s*(?<n>\d+)
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
    private static partial Regex GenericRegex();

    #endregion

    #region Checking

    /// <summary>
    ///   Whether a title has the shape of a generic episode title, whatever
    ///   its number.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <returns><c>true</c> for a generic title.</returns>
    internal static bool LooksGeneric(string? title)
        => !string.IsNullOrWhiteSpace(title) && GenericRegex().IsMatch(title.Trim());

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
        var match = GenericRegex().Match(title.Trim());
        return type is EpisodeType.Episode && match.Success && !match.Groups["t"].Success &&
            int.TryParse(match.Groups["n"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var found) && found == number;
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

    #endregion

    #region Making Up

    /// <summary>
    ///   Makes up a generic title for an episode, in the first of the given
    ///   languages that has a form for it, else in English.
    /// </summary>
    /// <param name="type">The episode's type. Only normal episodes have forms other than English.</param>
    /// <param name="number">The episode's number.</param>
    /// <param name="languages">The languages to try, best first.</param>
    /// <returns>The title, marked as made up.</returns>
    internal static ITitle Synthesize(EpisodeType type, int number, IEnumerable<TitleLanguage> languages)
    {
        var language = TitleLanguage.English;
        if (type is EpisodeType.Episode)
        {
            foreach (var candidate in languages)
            {
                if (!_normalForms.ContainsKey(candidate))
                    continue;

                language = candidate;
                break;
            }
        }

        var value = type is EpisodeType.Episode
            ? string.Format(CultureInfo.InvariantCulture, _normalForms[language], number)
            : $"{_typeWords.GetValueOrDefault(type, "Episode")} {number}";
        var (languageCode, countryCode) = language.GetLanguageAndCountryCode();
        return new SynthesizedTitle
        {
            Source = MetadataSource.Generated,
            Language = language,
            LanguageCode = languageCode,
            CountryCode = countryCode,
            Value = value,
            Type = TitleType.None,
        };
    }

    /// <summary>
    ///   A title made up on the spot.
    /// </summary>
    private sealed class SynthesizedTitle : TitleStub, ITitle
    {
        bool ITitle.IsSynthesized => true;
    }

    #endregion
}
