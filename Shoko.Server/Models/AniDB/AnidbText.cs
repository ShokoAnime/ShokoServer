using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Server.Utilities;

namespace Shoko.Server.Models.AniDB;

/// <summary>
///   Hands out the titles AniDB gave its anime and episodes as AniDB's
///   models always have, and synthesizes the generic title of an episode,
///   which is not stored.
/// </summary>
internal static class AnidbText
{
    #region Titles

    /// <summary>
    ///   A stored title as AniDB's models give it: with the code of its
    ///   language, whatever spelling of the code the row keeps.
    /// </summary>
    /// <remarks>
    ///   Rows older versions wrote may keep an upper-case code, such as
    ///   <c>EN</c>, which the models have always read as the language's own
    ///   code.
    /// </remarks>
    /// <param name="title">The title, or <c>null</c>.</param>
    /// <returns>The title, a copy of it with the language's code, or <c>null</c>.</returns>
    internal static ITitle? Present(ITitle? title)
    {
        if (title is not StoredTitle stored || title.Source != MetadataSource.AniDB)
            return title;

        var languageCode = stored.Language.GetString();
        if (string.Equals(stored.LanguageCode, languageCode, System.StringComparison.Ordinal))
            return stored;

        return new StoredTitle
        {
            ID = stored.ID,
            EntityID = stored.EntityID,
            Source = stored.Source,
            Language = stored.Language,
            LanguageCode = languageCode,
            CountryCode = stored.CountryCode,
            ScriptCode = stored.ScriptCode,
            Value = stored.Value,
            Type = stored.Type,
            IsEnabled = stored.IsEnabled,
            Preference = stored.Preference,
            Ordering = stored.Ordering,
            ReferenceID = stored.ReferenceID,
        };
    }

    /// <summary>
    ///   Stored titles as AniDB's models give them.
    /// </summary>
    /// <param name="titles">The titles.</param>
    /// <returns>The titles, each with the code of its language.</returns>
    internal static IReadOnlyList<ITitle> Present(IReadOnlyList<ITitle> titles)
        => titles.Count is 0 ? titles : [.. titles.Select(title => Present(title)!)];

    /// <summary>
    ///   The type an AniDB episode title is stored and read with: main for
    ///   English, as an episode's English title is its default, and none
    ///   for every other language.
    /// </summary>
    /// <param name="language">The title's language.</param>
    /// <returns>The type.</returns>
    internal static TitleType EpisodeTitleType(TitleLanguage language)
        => language is TitleLanguage.English ? TitleType.Main : TitleType.None;

    #endregion

    #region Generic Titles

    /// <summary>
    ///   AniDB's generic English title of an episode,
    ///   <c>Episode {prefix}{number}</c>, such as <c>Episode 5</c> or
    ///   <c>Episode S1</c>.
    /// </summary>
    /// <param name="type">The episode's type, which gives the prefix.</param>
    /// <param name="number">The episode's number.</param>
    /// <returns>The title's value.</returns>
    internal static string GenericEnglishValue(EpisodeType type, int number)
        => $"Episode {type.Prefix}{number}";

    /// <summary>
    ///   The title synthesized for an episode with no English title, as no
    ///   generic title is stored: a normal episode's generic title in the first
    ///   episode naming language with a form, such as <c>Episode 5</c> or
    ///   <c>第5話</c>, and AniDB's English form for the other types, such as
    ///   <c>Episode S1</c>.
    /// </summary>
    /// <remarks>
    ///   AniDB's main episode title is its English one, so <c>x-main</c> reads
    ///   as English.
    /// </remarks>
    /// <param name="type">The episode's type.</param>
    /// <param name="number">The episode's number.</param>
    /// <returns>The title, listed as the main one under AniDB and marked as synthesized.</returns>
    internal static ITitle SynthesizedTitle(EpisodeType type, int number)
        => GenericEpisodeTitles.Synthesize(
            type,
            number,
            true,
            Languages.PreferredEpisodeNamingLanguages.Select(language => language.Language is TitleLanguage.Main ? TitleLanguage.English : language.Language),
            TitleType.Main,
            MetadataSource.AniDB
        );

    #endregion
}
