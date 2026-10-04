using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Storage;
using TMDbLib.Objects.TvShows;

namespace Shoko.Plugin.Tmdb.Mapping;

/// <summary>
///   Turns TMDb's episode group collections into orderings of their show.
/// </summary>
/// <remarks>
///   Each collection is an ordering and each of its groups a season of it,
///   in TMDb's order. A group numbered <c>0</c> holds the specials. Only the
///   show's stored episodes are placed, as an ordering may name no other.
/// </remarks>
public static class TmdbOrderings
{
    /// <summary>
    ///   An episode group collection, as an ordering of its show.
    /// </summary>
    /// <param name="showID">The TMDb show ID.</param>
    /// <param name="collection">The collection, with its groups and episodes.</param>
    /// <param name="storedEpisodes">The show's stored episodes.</param>
    /// <returns>The ordering, or <c>null</c> when none of its episodes is stored.</returns>
    public static MetadataOrderingData? ToOrderingData(int showID, TvGroupCollection collection, IReadOnlySet<MetadataGuid> storedEpisodes)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(storedEpisodes);

        if (string.IsNullOrWhiteSpace(collection.Id))
            return null;

        var hasSpecials = false;
        var groups = new List<MetadataOrderingGroupData>();
        foreach (var group in (collection.Groups ?? []).Where(group => !string.IsNullOrWhiteSpace(group.Id)).OrderBy(group => group.Order))
        {
            var isSpecial = group.Order is 0 && !hasSpecials;
            hasSpecials |= isSpecial;
            groups.Add(new()
            {
                ID = TmdbIds.OrderingGroup(group.Id!),
                Name = TmdbTexts.Clean(group.Name) ?? string.Empty,
                IsSpecial = isSpecial,
                Episodes =
                [
                    .. (group.Episodes ?? [])
                        .OrderBy(episode => episode.Order)
                        .Select(episode => episode.Id is { } id ? TmdbIds.Episode(id) : null)
                        .OfType<MetadataGuid>()
                        .Where(storedEpisodes.Contains)
                        .Distinct(),
                ],
            });
        }

        if (groups.All(group => group.Episodes.Count is 0))
            return null;

        return new()
        {
            ID = TmdbIds.Ordering(collection.Id),
            SeriesID = TmdbIds.Series(showID),
            Name = TmdbTexts.Clean(collection.Name) ?? collection.Id,
            Overview = TmdbTexts.Clean(collection.Description),
            Type = TypeOf(collection.Type),
            Networks = collection.Network is { Id: > 0 } network ? [TmdbIds.Network(network.Id)] : [],
            Groups = groups,
        };
    }

    /// <summary>
    ///   What an ordering follows, from TMDb's kind of collection.
    /// </summary>
    /// <param name="type">TMDb's kind.</param>
    /// <returns>The ordering type.</returns>
    public static OrderingType TypeOf(TvGroupType type)
        => type switch
        {
            TvGroupType.OriginalAirDate => OrderingType.OriginalAirDate,
            TvGroupType.Absolute => OrderingType.Absolute,
            TvGroupType.DVD => OrderingType.DVD,
            TvGroupType.Digital => OrderingType.Digital,
            TvGroupType.StoryArc => OrderingType.StoryArc,
            TvGroupType.Production => OrderingType.Production,
            TvGroupType.TV => OrderingType.TV,
            _ => OrderingType.Unknown,
        };
}
