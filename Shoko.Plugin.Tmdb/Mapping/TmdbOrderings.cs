using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Storage;
using TMDbLib.Objects.TvShows;

namespace Shoko.Plugin.Tmdb.Mapping;

/// <summary>
///   Turns TMDB's episode group collections into orderings of their show.
/// </summary>
/// <remarks>
///   Each collection is an ordering and each of its groups a season of it,
///   in TMDB's order and numbered as TMDB orders it. The first group
///   numbered <c>0</c> holds the specials. Only the show's stored episodes
///   are placed, as an ordering may name no other. TMDB gives the names and
///   descriptions in American English.
/// </remarks>
public static class TmdbOrderings
{
    /// <summary>
    ///   An episode group collection, as an ordering of its show.
    /// </summary>
    /// <param name="showID">The TMDB show ID.</param>
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
                Titles = Titles(group.Name),
                IsSpecial = isSpecial,
                SeasonNumber = !isSpecial && group.Order > 0 ? group.Order : null,
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
            Titles = Titles(collection.Name),
            Overviews = TmdbTexts.Clean(collection.Description) is { } description ? [TmdbTexts.Overview("en", "US", description)] : [],
            Type = TypeOf(collection.Type),
            Networks = collection.Network is { Id: > 0 } network ? [TmdbIds.Network(network.Id)] : [],
            Groups = groups,
        };
    }

    /// <summary>
    ///   The titles of a collection or group: its American English name as its
    ///   main title, or none when it has no name.
    /// </summary>
    /// <param name="name">The name TMDB gave it.</param>
    /// <returns>The titles.</returns>
    private static IReadOnlyList<ITitle> Titles(string? name)
        => TmdbTexts.Clean(name) is { } value ? [TmdbTexts.Title("en", "US", value, TitleType.Main)] : [];

    /// <summary>
    ///   What an ordering follows, from TMDB's kind of collection.
    /// </summary>
    /// <param name="type">TMDB's kind.</param>
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
