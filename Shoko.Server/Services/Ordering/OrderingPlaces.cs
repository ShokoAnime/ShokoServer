using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Services.Ordering;

/// <summary>
///   An ordering's places once <see cref="OrderingPlacement"/> placed its
///   specials, in the season and episode numbers the ordering serves. Holds
///   IDs only, so it can be kept while the entries change.
/// </summary>
internal sealed class OrderingPlaces
{
    private readonly IReadOnlyList<OrderingPlacesGroup> _groups;

    private readonly Dictionary<MetadataGuid, int> _groupIndexes;

    private readonly IReadOnlyDictionary<MetadataGuid, int>? _ownNumbers;

    private readonly Dictionary<MetadataGuid, List<OrderingPlace>> _places = [];

    private readonly Dictionary<MetadataGuid, int> _specialNumbers = [];

    private readonly Dictionary<MetadataGuid, OrderingAiring> _airings = [];

    private readonly Dictionary<int, IReadOnlyList<PlacedEpisode>> _homes;

    private readonly int? _specialIndex;

    #region Building

    /// <summary>
    ///   Places an ordering.
    /// </summary>
    /// <param name="groups">The ordering's groups, in order. Only the first special one counts as special.</param>
    /// <param name="ownNumbers">Each episode's own number, for the default ordering; <c>null</c> numbers episodes by their place.</param>
    /// <param name="trailing">Episodes in no group, watched after all others, for the default ordering.</param>
    private OrderingPlaces(IReadOnlyList<OrderingPlacesGroup> groups, IReadOnlyDictionary<MetadataGuid, int>? ownNumbers, IReadOnlyList<MetadataGuid> trailing)
    {
        var specialIndex = groups.Select((group, index) => (group, index)).FirstOrDefault(pair => pair.group.IsSpecial, (null!, -1)).index;
        _specialIndex = specialIndex >= 0 ? specialIndex : null;
        _groups = groups;
        _ownNumbers = ownNumbers;
        _groupIndexes = [];
        for (var index = 0; index < groups.Count; index++)
            _groupIndexes.TryAdd(groups[index].ID, index);

        Result = OrderingPlacement.Place([.. groups.Select((group, index) => new OrderingPlacementGroup(index == _specialIndex, group.EpisodeIDs))]);
        _homes = Result.Groups.ToDictionary(group => group.GroupIndex, group => group.Episodes);
        foreach (var group in Result.Groups)
        {
            foreach (var episode in group.Episodes)
            {
                var number = Number(episode.EpisodeID, episode.EpisodeNumber);
                if (group.IsSpecial)
                    _specialNumbers.TryAdd(episode.EpisodeID, number);
                if (!_places.TryGetValue(episode.EpisodeID, out var list))
                    _places[episode.EpisodeID] = list = [];
                list.Add(new(groups[group.GroupIndex].ID, number, group.IsSpecial));
            }
        }

        foreach (var special in Result.Specials)
            _airings[special.EpisodeID] = Airing(special);

        var seen = Result.ViewingOrder.ToHashSet();
        ViewingOrder = [.. Result.ViewingOrder, .. trailing.Where(seen.Add)];
    }

    /// <summary>
    ///   Places an ordering whose groups number their episodes by place, as
    ///   a stored ordering or a source's own.
    /// </summary>
    /// <param name="groups">The ordering's groups, in order.</param>
    /// <returns>The places.</returns>
    internal static OrderingPlaces ByPlace(IReadOnlyList<OrderingPlacesGroup> groups)
        => new(groups, null, []);

    /// <summary>
    ///   Places a default ordering, whose episodes keep their own numbers.
    /// </summary>
    /// <param name="groups">The series' seasons, in order, with the airing places of its placed specials.</param>
    /// <param name="ownNumbers">Each episode's own number.</param>
    /// <param name="trailing">The episodes in no season, watched after all others.</param>
    /// <returns>The places.</returns>
    internal static OrderingPlaces ByOwnNumbers(
        IReadOnlyList<OrderingPlacesGroup> groups,
        IReadOnlyDictionary<MetadataGuid, int> ownNumbers,
        IReadOnlyList<MetadataGuid> trailing
    )
        => new(groups, ownNumbers, trailing);

    #endregion

    #region Reading

    /// <summary>
    ///   The placement the places come from.
    /// </summary>
    internal OrderingPlacementResult Result { get; }

    /// <summary>
    ///   Every episode once, in viewing order: placed specials where they air.
    /// </summary>
    internal IReadOnlyList<MetadataGuid> ViewingOrder { get; }

    /// <summary>
    ///   How many distinct episodes the ordering has.
    /// </summary>
    internal int EpisodeCount => ViewingOrder.Count;

    /// <summary>
    ///   The episodes at home in a group, in order.
    /// </summary>
    /// <param name="groupID">The group.</param>
    /// <returns>The episodes, or none when the ordering has no such group.</returns>
    internal IReadOnlyList<MetadataGuid> HomeEpisodes(MetadataGuid groupID)
        => _groupIndexes.TryGetValue(groupID, out var index) && _homes.TryGetValue(index, out var homes)
            ? [.. homes.Select(episode => episode.EpisodeID)]
            : [];

    /// <summary>
    ///   Every place an episode has: one in the special group for a placed
    ///   special, and its home places for any other.
    /// </summary>
    /// <param name="episodeID">The episode.</param>
    /// <returns>The places, in group order.</returns>
    internal IReadOnlyList<OrderingPlace> PlacesOf(MetadataGuid episodeID)
        => _places.TryGetValue(episodeID, out var places) ? places : [];

    /// <summary>
    ///   Where a placed special airs.
    /// </summary>
    /// <param name="episodeID">The episode.</param>
    /// <returns>Where it airs, or <c>null</c> when it is not a placed special.</returns>
    internal OrderingAiring? AiringOf(MetadataGuid episodeID)
        => _airings.GetValueOrDefault(episodeID);

    /// <summary>
    ///   Every entry a group lists, in order: its home episodes with their
    ///   numbers, and the placed specials airing there, flagged as specials
    ///   with their special group numbers. The special group's entries carry
    ///   where they air.
    /// </summary>
    /// <param name="groupID">The group.</param>
    /// <returns>The entries, or none when the ordering has no such group.</returns>
    internal IReadOnlyList<ListedPlace> Listed(MetadataGuid groupID)
    {
        if (!_groupIndexes.TryGetValue(groupID, out var index))
            return [];

        var homes = _homes.GetValueOrDefault(index) ?? [];
        if (index == _specialIndex)
            return [.. homes.Select(episode => new ListedPlace(episode.EpisodeID, _specialNumbers[episode.EpisodeID], true, AiringOf(episode.EpisodeID)))];

        var listed = new List<ListedPlace>();
        var home = 0;
        foreach (var id in _groups[index].EpisodeIDs)
        {
            if (_specialNumbers.TryGetValue(id, out var specialNumber))
                listed.Add(new(id, specialNumber, true, null));
            else if (home < homes.Count)
                listed.Add(new(id, Number(id, homes[home++].EpisodeNumber), false, null));
        }

        return listed;
    }

    #endregion

    #region Helpers

    /// <summary>
    ///   The number an episode is served with.
    /// </summary>
    /// <param name="episodeID">The episode.</param>
    /// <param name="placed">Its number in the placement.</param>
    /// <returns>Its own number for the default ordering, else the placement's.</returns>
    private int Number(MetadataGuid episodeID, int placed)
        => _ownNumbers is not null && _ownNumbers.TryGetValue(episodeID, out var own) ? own : placed;

    /// <summary>
    ///   Where a placed special airs, in the numbers the ordering serves.
    /// </summary>
    /// <param name="special">The placed special.</param>
    /// <returns>Where it airs.</returns>
    private OrderingAiring Airing(PlacedSpecial special)
    {
        int? beforeSeason = null, beforeEpisode = null, afterSeason = null;
        if (special.AirsBeforeSeasonNumber is { } before && GroupOf(before) is { } beforeGroup)
        {
            beforeSeason = _groups[beforeGroup].SeasonNumber;
            beforeEpisode = special.NextEpisodeID is { } next ? Number(next, special.AirsBeforeEpisodeNumber ?? 0) : special.AirsBeforeEpisodeNumber;
        }

        if (special.AirsAfterSeasonNumber is { } after && GroupOf(after) is { } afterGroup)
            afterSeason = _groups[afterGroup].SeasonNumber;

        return new(beforeSeason, beforeEpisode, afterSeason, special.PreviousEpisodeID, special.NextEpisodeID);
    }

    /// <summary>
    ///   The index of the regular group the placement numbered as a season.
    /// </summary>
    /// <param name="placedSeasonNumber">The placement's season number.</param>
    /// <returns>The group's index, or <c>null</c>.</returns>
    private int? GroupOf(int placedSeasonNumber)
        => Result.Groups.FirstOrDefault(group => !group.IsSpecial && group.SeasonNumber == placedSeasonNumber)?.GroupIndex;

    #endregion
}

/// <summary>
///   A group of an ordering to place.
/// </summary>
/// <param name="ID">The group, read as a season.</param>
/// <param name="SeasonNumber">The season number the ordering serves for it.</param>
/// <param name="IsSpecial">Whether it is the special group.</param>
/// <param name="EpisodeIDs">The episodes it lists, in order, placed specials at their airing places.</param>
internal sealed record OrderingPlacesGroup(MetadataGuid ID, int SeasonNumber, bool IsSpecial, IReadOnlyList<MetadataGuid> EpisodeIDs);

/// <summary>
///   One place of an episode.
/// </summary>
/// <param name="GroupID">The group it is in.</param>
/// <param name="EpisodeNumber">Its number there, as the ordering serves it.</param>
/// <param name="IsSpecial">Whether the group is the special group.</param>
internal sealed record OrderingPlace(MetadataGuid GroupID, int EpisodeNumber, bool IsSpecial);

/// <summary>
///   One entry a group lists.
/// </summary>
/// <param name="EpisodeID">The episode.</param>
/// <param name="EpisodeNumber">Its number: in the group for a home episode, in the special group for a special.</param>
/// <param name="IsSpecial">Whether it is a special: in the special group, or a placed special airing in a regular one.</param>
/// <param name="Airing">Where a placed special airs, on its special group entry only.</param>
internal sealed record ListedPlace(MetadataGuid EpisodeID, int EpisodeNumber, bool IsSpecial, OrderingAiring? Airing);

/// <summary>
///   Where a placed special airs, in the numbers its ordering serves.
/// </summary>
/// <param name="AirsBeforeSeasonNumber">The season of the regular episode it airs before, when one of its group follows.</param>
/// <param name="AirsBeforeEpisodeNumber">The number of that episode.</param>
/// <param name="AirsAfterSeasonNumber">The season it airs after, when none of its group follows.</param>
/// <param name="AirsAfterEpisodeID">The regular episode right before it, in any group.</param>
/// <param name="AirsBeforeEpisodeID">The regular episode right after it, in any group.</param>
internal sealed record OrderingAiring(
    int? AirsBeforeSeasonNumber,
    int? AirsBeforeEpisodeNumber,
    int? AirsAfterSeasonNumber,
    MetadataGuid? AirsAfterEpisodeID,
    MetadataGuid? AirsBeforeEpisodeID
);

/// <summary>
///   An ordering that places its specials, so its groups can be listed
///   with every place they hold.
/// </summary>
internal interface IPlacedOrdering
{
    /// <summary>
    ///   The ordering's places, keyed by the IDs of its seasons.
    /// </summary>
    OrderingPlaces Placement { get; }
}
