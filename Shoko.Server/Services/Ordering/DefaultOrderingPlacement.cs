using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Services.Ordering;

/// <summary>
///   Places the specials of a series' default ordering without moving them
///   out of season 0: an AniDB anime's or a Shoko series' by their AniDB
///   titles, and a plugin source's by the airing places its provider gave.
/// </summary>
internal static class DefaultOrderingPlacement
{
    #region Building

    /// <summary>
    ///   Places a series' default ordering: its seasons in order, each
    ///   episode by its own number, the episodes in no season last.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The places, keyed by the series' season IDs.</returns>
    internal static OrderingPlaces Build(ISeries series)
    {
        var seasons = series.Seasons.DistinctBy(season => season.ID).ToList();
        var seasonIndexes = seasons.Select((season, index) => (season.ID, index)).ToDictionary(pair => pair.ID, pair => pair.index);
        var episodes = series.Episodes.DistinctBy(episode => episode.ID).ToList();
        var lists = seasons.Select(_ => new List<IEpisode>()).ToList();
        var trailing = new List<IEpisode>();
        foreach (var episode in episodes.OrderBy(episode => episode.EpisodeNumber))
        {
            if (episode.SeasonID is { } seasonID && seasonIndexes.TryGetValue(seasonID, out var index))
                lists[index].Add(episode);
            else
                trailing.Add(episode);
        }

        var specialIndex = seasons.FindIndex(season => season.IsSpecial);
        var airings = specialIndex < 0
            ? []
            : series.ID.Source == MetadataSource.AniDB || series.ID.Source == MetadataSource.Shoko
                ? FromTitles(seasons, lists, specialIndex)
                : series.ID.Source.IsCore ? [] : FromProvider(seasons, lists, specialIndex);

        var groups = new List<OrderingPlacesGroup>();
        for (var index = 0; index < seasons.Count; index++)
        {
            var listed = new List<MetadataGuid>();
            var inserts = airings.Where(airing => airing.GroupIndex == index).ToList();
            foreach (var episode in lists[index])
            {
                listed.AddRange(inserts.Where(airing => airing.BeforeEpisodeID == episode.ID).Select(airing => airing.SpecialID));
                listed.Add(episode.ID);
            }

            listed.AddRange(inserts.Where(airing => airing.BeforeEpisodeID is null).Select(airing => airing.SpecialID));
            groups.Add(new(seasons[index].ID, seasons[index].SeasonNumber, index == specialIndex, listed));
        }

        return OrderingPlaces.ByOwnNumbers(
            groups,
            episodes.ToDictionary(episode => episode.ID, episode => episode.EpisodeNumber),
            [.. trailing.Select(episode => episode.ID)]
        );
    }

    #endregion

    #region AniDB

    /// <summary>
    ///   Places the specials whose AniDB titles say where they air in the
    ///   first regular season, in airing order.
    /// </summary>
    /// <param name="seasons">The series' seasons.</param>
    /// <param name="lists">Each season's episodes, by number.</param>
    /// <param name="specialIndex">The index of the specials' season.</param>
    /// <returns>Where each placed special airs.</returns>
    private static List<Airing> FromTitles(IReadOnlyList<ISeason> seasons, IReadOnlyList<List<IEpisode>> lists, int specialIndex)
    {
        var regularIndex = Enumerable.Range(0, seasons.Count).FirstOrDefault(index => index != specialIndex && lists[index].Count > 0, -1);
        if (regularIndex < 0)
            return [];

        var regulars = lists[regularIndex].Where(episode => episode.Type == EpisodeType.Episode).ToList();
        var regularCount = regulars.Count is 0 ? 0 : regulars.Max(episode => episode.EpisodeNumber);
        var hinted = lists[specialIndex]
            .Select(special => (Special: special, Hint: Hint(special, regularCount)))
            .Where(pair => pair.Hint is not null)
            .Select(pair => (pair.Special, Hint: pair.Hint!.Value));
        return
        [
            .. AnidbSpecialTitleHints.InAiringOrder(hinted, pair => pair.Hint, pair => pair.Special.EpisodeNumber)
                .Select(pair => new Airing(
                    pair.Special.ID,
                    regularIndex,
                    regulars.FirstOrDefault(episode => episode.EpisodeNumber > pair.Hint.AfterEpisode)?.ID
                )),
        ];
    }

    /// <summary>
    ///   Reads where a special airs from its AniDB titles.
    /// </summary>
    /// <param name="special">The special, of an AniDB anime or a Shoko series.</param>
    /// <param name="regularCount">How many regular episodes the anime has.</param>
    /// <returns>The hint, or <c>null</c>.</returns>
    private static AnidbSpecialTitleHint? Hint(IEpisode special, int regularCount)
    {
        IEpisode? anidb = special switch
        {
            AnimeEpisode shoko => shoko.AniDB_Episode,
            IShokoEpisode shoko => shoko.AnidbEpisode,
            _ => special,
        };
        if (anidb is null)
            return null;

        var titles = anidb.Titles.Where(title => title.Source == MetadataSource.AniDB).ToList();
        return AnidbSpecialTitleHints.Parse(
            titles.FirstOrDefault(title => title.Language == TitleLanguage.English)?.Value,
            titles.FirstOrDefault(title => title.Language == TitleLanguage.Romaji)?.Value,
            titles.FirstOrDefault(title => title.Language == TitleLanguage.Japanese)?.Value,
            regularCount
        );
    }

    #endregion

    #region Plugin Sources

    /// <summary>
    ///   Places the season 0 episodes of a plugin source's series where its
    ///   provider said they air, ignoring targets the series does not have.
    ///   A season to air before with no episode means its first episode.
    /// </summary>
    /// <param name="seasons">The series' seasons.</param>
    /// <param name="lists">Each season's episodes, by number.</param>
    /// <param name="specialIndex">The index of the specials' season.</param>
    /// <returns>Where each placed special airs.</returns>
    private static List<Airing> FromProvider(IReadOnlyList<ISeason> seasons, IReadOnlyList<List<IEpisode>> lists, int specialIndex)
    {
        var airings = new List<Airing>();
        foreach (var special in lists[specialIndex].OfType<Metadata_Episode>())
        {
            if (special.AirsBeforeSeasonNumber is { } beforeSeason && Regular(beforeSeason) is { } beforeIndex)
            {
                var target = special.AirsBeforeEpisodeNumber is { } beforeEpisode
                    ? lists[beforeIndex].FirstOrDefault(episode => episode.EpisodeNumber == beforeEpisode)
                    : lists[beforeIndex].FirstOrDefault();
                if (target is not null)
                {
                    airings.Add(new(special.ID, beforeIndex, target.ID));
                    continue;
                }
            }

            if (special.AirsAfterSeasonNumber is { } afterSeason && Regular(afterSeason) is { } afterIndex)
                airings.Add(new(special.ID, afterIndex, null));
        }

        return airings;

        int? Regular(int seasonNumber)
        {
            var index = Enumerable.Range(0, seasons.Count).FirstOrDefault(index => index != specialIndex && seasons[index].SeasonNumber == seasonNumber, -1);
            return index < 0 ? null : index;
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    ///   Where a special airs in the default ordering.
    /// </summary>
    /// <param name="SpecialID">The special.</param>
    /// <param name="GroupIndex">The season it airs in.</param>
    /// <param name="BeforeEpisodeID">The episode of that season it airs before, or <c>null</c> for after its last.</param>
    private sealed record Airing(MetadataGuid SpecialID, int GroupIndex, MetadataGuid? BeforeEpisodeID);

    #endregion
}
