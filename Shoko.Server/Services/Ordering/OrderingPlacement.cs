using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Services.Ordering;

/// <summary>
///   Places the specials of an ordering. An episode in the special group
///   (season 0) and in a regular group belongs to season 0; its regular
///   place only says where it airs.
/// </summary>
/// <remarks>
///   An episode in regular groups only keeps every place it has. A special
///   with several regular places airs at the first one, in group order. The
///   specials with no regular place are watched where the special group
///   sits among the groups.
/// </remarks>
internal static class OrderingPlacement
{
    #region Placement

    /// <summary>
    ///   Places the episodes of an ordering's groups.
    /// </summary>
    /// <param name="groups">The ordering's groups, in order. At most one may be special.</param>
    /// <returns>The home of every episode, the placed specials and the viewing order.</returns>
    /// <exception cref="ArgumentException">More than one group is special.</exception>
    internal static OrderingPlacementResult Place(IReadOnlyList<OrderingPlacementGroup> groups)
    {
        if (groups.Count(group => group.IsSpecial) > 1)
            throw new ArgumentException("An ordering has at most one special group.", nameof(groups));

        var specialGroup = groups.FirstOrDefault(group => group.IsSpecial);
        var specialIDs = new List<MetadataGuid>();
        var specialNumbers = new Dictionary<MetadataGuid, int>();
        foreach (var id in specialGroup?.EpisodeIDs ?? [])
        {
            if (specialNumbers.TryAdd(id, specialIDs.Count + 1))
                specialIDs.Add(id);
        }

        var seasonNumber = 0;
        var seasonNumbers = groups.Select(group => group.IsSpecial ? 0 : ++seasonNumber).ToArray();

        // Every place in the regular groups, in order, with the number of each home episode.
        var places = new List<RegularPlace>();
        var airingPlaces = new Dictionary<MetadataGuid, int>();
        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            if (groups[groupIndex].IsSpecial)
                continue;

            var episodeNumber = 0;
            foreach (var id in groups[groupIndex].EpisodeIDs)
            {
                if (!specialNumbers.ContainsKey(id))
                {
                    places.Add(new(groupIndex, seasonNumbers[groupIndex], id, ++episodeNumber));
                    continue;
                }

                if (airingPlaces.TryAdd(id, places.Count))
                    places.Add(new(groupIndex, seasonNumbers[groupIndex], id, null));
            }
        }

        var specials = new Dictionary<MetadataGuid, PlacedSpecial>();
        foreach (var (id, index) in airingPlaces)
        {
            var place = places[index];
            var previous = places.Take(index).LastOrDefault(item => item.EpisodeNumber is not null);
            var next = places.Skip(index + 1).FirstOrDefault(item => item.EpisodeNumber is not null);
            var nextInSeason = next?.GroupIndex == place.GroupIndex ? next : null;
            specials[id] = new(
                id,
                specialNumbers[id],
                nextInSeason?.SeasonNumber,
                nextInSeason?.EpisodeNumber,
                nextInSeason is null ? place.SeasonNumber : null,
                previous?.EpisodeID,
                next?.EpisodeID
            );
        }

        var placedGroups = new List<PlacedGroup>();
        var viewingOrder = new List<MetadataGuid>();
        var seen = new HashSet<MetadataGuid>();
        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            if (groups[groupIndex].IsSpecial)
            {
                placedGroups.Add(new(groupIndex, true, 0, [.. specialIDs.Select(id => new PlacedEpisode(id, specialNumbers[id]))]));
                viewingOrder.AddRange(specialIDs.Where(id => !airingPlaces.ContainsKey(id) && seen.Add(id)));
                continue;
            }

            var groupPlaces = places.Where(place => place.GroupIndex == groupIndex).ToList();
            placedGroups.Add(new(
                groupIndex,
                false,
                seasonNumbers[groupIndex],
                [.. groupPlaces.Where(place => place.EpisodeNumber is not null).Select(place => new PlacedEpisode(place.EpisodeID, place.EpisodeNumber!.Value))]
            ));
            viewingOrder.AddRange(groupPlaces.Select(place => place.EpisodeID).Where(seen.Add));
        }

        return new(placedGroups, [.. viewingOrder.Where(specials.ContainsKey).Select(id => specials[id])], viewingOrder, viewingOrder.Count);
    }

    #endregion

    #region Helpers

    /// <summary>
    ///   One place in a regular group.
    /// </summary>
    /// <param name="GroupIndex">The group's index in the ordering.</param>
    /// <param name="SeasonNumber">The group's season number.</param>
    /// <param name="EpisodeID">The episode at the place.</param>
    /// <param name="EpisodeNumber">The episode's number, or <c>null</c> for a placed special.</param>
    private sealed record RegularPlace(int GroupIndex, int SeasonNumber, MetadataGuid EpisodeID, int? EpisodeNumber);

    #endregion
}

/// <summary>
///   A group of an ordering, as given to <see cref="OrderingPlacement.Place"/>.
/// </summary>
/// <param name="IsSpecial">Whether the group is the special group, season 0.</param>
/// <param name="EpisodeIDs">The group's episodes, in order.</param>
internal sealed record OrderingPlacementGroup(bool IsSpecial, IReadOnlyList<MetadataGuid> EpisodeIDs);

/// <summary>
///   An ordering's episodes once its specials are placed.
/// </summary>
/// <param name="Groups">Every group with its home episodes, in the ordering's order.</param>
/// <param name="Specials">The placed specials, in viewing order.</param>
/// <param name="ViewingOrder">Every episode once, placed specials where they air.</param>
/// <param name="EpisodeCount">How many distinct episodes the ordering has.</param>
internal sealed record OrderingPlacementResult(
    IReadOnlyList<PlacedGroup> Groups,
    IReadOnlyList<PlacedSpecial> Specials,
    IReadOnlyList<MetadataGuid> ViewingOrder,
    int EpisodeCount
);

/// <summary>
///   A group and the episodes it is home to.
/// </summary>
/// <param name="GroupIndex">The group's index in the ordering.</param>
/// <param name="IsSpecial">Whether it is the special group.</param>
/// <param name="SeasonNumber">0 for the special group, else 1 to N in the ordering's order.</param>
/// <param name="Episodes">The home episodes, numbered from 1, placed specials left out.</param>
internal sealed record PlacedGroup(int GroupIndex, bool IsSpecial, int SeasonNumber, IReadOnlyList<PlacedEpisode> Episodes);

/// <summary>
///   An episode at its home, with its number there.
/// </summary>
/// <param name="EpisodeID">The episode.</param>
/// <param name="EpisodeNumber">Its number in the group, from 1.</param>
internal sealed record PlacedEpisode(MetadataGuid EpisodeID, int EpisodeNumber);

/// <summary>
///   A special with a place in a regular group. It is a special at home, and
///   airs before the next regular episode of that group, or after the group
///   when none follows.
/// </summary>
/// <param name="EpisodeID">The special.</param>
/// <param name="SpecialNumber">Its number in the special group, from 1.</param>
/// <param name="AirsBeforeSeasonNumber">The season of the episode it airs before, if any.</param>
/// <param name="AirsBeforeEpisodeNumber">The number of the episode it airs before, if any.</param>
/// <param name="AirsAfterSeasonNumber">The season it airs after, when no episode of it follows.</param>
/// <param name="PreviousEpisodeID">The regular episode before it, in any group, if any.</param>
/// <param name="NextEpisodeID">The regular episode after it, in any group, if any.</param>
internal sealed record PlacedSpecial(
    MetadataGuid EpisodeID,
    int SpecialNumber,
    int? AirsBeforeSeasonNumber,
    int? AirsBeforeEpisodeNumber,
    int? AirsAfterSeasonNumber,
    MetadataGuid? PreviousEpisodeID,
    MetadataGuid? NextEpisodeID
);
