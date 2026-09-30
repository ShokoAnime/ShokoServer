using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Services;

namespace Shoko.Server.Providers.AniDB;

/// <summary>
///   Works out which of the titles AniDB lists for an anime or an episode
///   are stored, and in what order.
/// </summary>
/// <remarks>
///   The titles are stored in AniDB's own order, the order its XML lists
///   them in, on every import; titles already stored keep their language
///   code.
/// </remarks>
internal static class AnidbTextListing
{
    #region Planning

    /// <summary>
    ///   The titles to store for an anime.
    /// </summary>
    /// <remarks>
    ///   A title is one type, language and value; empty titles and repeats
    ///   are left out.
    /// </remarks>
    /// <param name="stored">The anime's stored titles from AniDB, whose language codes are kept.</param>
    /// <param name="listed">What AniDB lists now, in its order.</param>
    /// <returns>The titles to store, in AniDB's order.</returns>
    internal static IReadOnlyList<ITitle> PlanAnimeTitles(IReadOnlyList<ITitle> stored, IEnumerable<ListedTitle> listed)
        => Plan(
            stored,
            listed.Where(title => !string.IsNullOrEmpty(title.Value)),
            title => (title.Type, title.Language, title.Value)
        );

    /// <summary>
    ///   The titles to store for an episode.
    /// </summary>
    /// <remarks>
    ///   A title is one language and value; repeats are left out, and so is
    ///   a generic title with the episode's own type and number, such as
    ///   <c>Episode 5</c>, which is made up when read instead.
    /// </remarks>
    /// <param name="stored">The episode's stored titles from AniDB, whose language codes are kept.</param>
    /// <param name="listed">What AniDB lists now, in its order. Their types are ignored.</param>
    /// <param name="type">The episode's type.</param>
    /// <param name="number">The episode's number.</param>
    /// <returns>The titles to store, in AniDB's order.</returns>
    internal static IReadOnlyList<ITitle> PlanEpisodeTitles(IReadOnlyList<ITitle> stored, IEnumerable<ListedTitle> listed, EpisodeType type, int number)
        => Plan(
            stored,
            listed
                .Where(title => !IsGeneric(title.Value, type, number))
                .Select(title => title with { Type = AnidbText.EpisodeTitleType(title.Language) }),
            title => (TitleType.None, title.Language, title.Value)
        );

    /// <summary>
    ///   Whether an episode title is left out of the stored ones: the generic
    ///   title of the episode's own type and number.
    /// </summary>
    /// <param name="value">The title.</param>
    /// <param name="type">The episode's type.</param>
    /// <param name="number">The episode's number.</param>
    /// <returns><c>true</c> when it is not stored.</returns>
    internal static bool IsGeneric(string? value, EpisodeType type, int number)
        => GenericEpisodeTitles.IsGeneric(value, type, number);

    /// <summary>
    ///   Lists the titles AniDB lists now in its order, each stored one with
    ///   the language code it was stored with.
    /// </summary>
    /// <param name="stored">The stored titles, whose language codes are kept.</param>
    /// <param name="listed">What AniDB lists now, in its order.</param>
    /// <param name="keyOf">What makes two titles the same.</param>
    /// <returns>The titles to store, in AniDB's order.</returns>
    private static IReadOnlyList<ITitle> Plan(
        IReadOnlyList<ITitle> stored,
        IEnumerable<ListedTitle> listed,
        Func<ListedTitle, (TitleType, TitleLanguage, string)> keyOf
    )
    {
        var storedCodes = new Dictionary<(TitleType, TitleLanguage, string), string>();
        foreach (var title in stored)
            storedCodes.TryAdd(keyOf(new(title.Language, title.Type, title.Value)), title.LanguageCode);

        var titles = new List<ITitle>();
        var seen = new HashSet<(TitleType, TitleLanguage, string)>();
        foreach (var title in listed)
        {
            var key = keyOf(title);
            if (seen.Add(key))
                titles.Add(Title(title, storedCodes.GetValueOrDefault(key) ?? title.Language.GetString()));
        }

        return titles;
    }

    /// <summary>
    ///   A title to store.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="languageCode">The language code to store it with.</param>
    /// <returns>The title.</returns>
    private static TitleStub Title(ListedTitle title, string languageCode)
        => new()
        {
            Source = MetadataSource.AniDB,
            Language = title.Language,
            LanguageCode = languageCode,
            Value = title.Value,
            Type = title.Type,
        };

    #endregion

    #region Listed Titles

    /// <summary>
    ///   A title as AniDB lists it.
    /// </summary>
    /// <param name="Language">The language.</param>
    /// <param name="Type">The kind of title.</param>
    /// <param name="Value">The title itself.</param>
    internal sealed record ListedTitle(TitleLanguage Language, TitleType Type, string Value);

    #endregion
}
