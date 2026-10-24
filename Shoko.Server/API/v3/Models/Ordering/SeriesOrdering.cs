using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.API.Converters;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Metadata;
using Shoko.Server.Services;

namespace Shoko.Server.API.v3.Models.Ordering;

/// <summary>
/// An ordering of a series' episodes into groups: its default one, made from
/// its seasons, one a plugin saved, or one a user made on this server.
/// </summary>
/// <param name="ordering">The ordering.</param>
/// <param name="includeGroups">Whether to include its groups and their episodes.</param>
/// <param name="models">Builds the models of its networks.</param>
public class SeriesOrdering(IOrdering ordering, bool includeGroups, MetadataModelBuilder models)
{
    /// <summary>
    /// The ordering's full ID, e.g. <c>user://ordering/1f0c…</c>.
    /// </summary>
    [Required]
    public string ID { get; init; } = ordering.ID.ToString();

    /// <summary>
    /// The ordering's ID within its source. For a user's ordering the ordering
    /// endpoints take this; any ordering can be fetched by its full
    /// <see cref="ID"/> (URL-encoded), and the default one also by <c>default</c>.
    /// </summary>
    [Required]
    public string LocalID { get; init; } = ordering.ID.ID;

    /// <summary>
    /// The source the ordering is from.
    /// </summary>
    [Required]
    public string Source { get; init; } = LegacyMetadataSpellings.Of(ordering.ID.Source);

    /// <summary>
    /// The ordering's name.
    /// </summary>
    [Required]
    public string Name { get; init; } = ordering.Title;

    /// <summary>
    /// What the ordering is about, or an empty string.
    /// </summary>
    [Required]
    public string Description { get; init; } = ordering.PreferredOverview?.Value ?? string.Empty;

    /// <summary>
    /// What the ordering follows.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public OrderingType Type { get; init; } = ordering.Type;

    /// <summary>
    /// Whether this is the series' default ordering.
    /// </summary>
    [Required]
    public bool IsDefault { get; init; } = ordering.IsDefault;

    /// <summary>
    /// Whether this is the ordering chosen for the series.
    /// </summary>
    [Required]
    public bool IsPreferred { get; init; } = ordering.IsPreferred;

    /// <summary>
    /// Whether a user made the ordering on this server, so it can be changed
    /// through the API.
    /// </summary>
    [Required]
    public bool IsLocal { get; init; } = ordering.ID.Source == MetadataSource.User;

    /// <summary>
    /// How many episodes the ordering holds, each counted once.
    /// </summary>
    [Required]
    public int EpisodeCount { get; init; } = ordering.EpisodeCount;

    /// <summary>
    /// How many of the ordering's episodes are hidden.
    /// </summary>
    [Required]
    public int HiddenEpisodeCount { get; init; } = ordering.HiddenEpisodeCount;

    /// <summary>
    /// How many groups the ordering has.
    /// </summary>
    [Required]
    public int SeasonCount { get; init; } = ordering.SeasonCount;

    /// <summary>
    /// The networks the ordering follows. The default ordering's are the
    /// series' own.
    /// </summary>
    [Required]
    public IReadOnlyList<MetadataNetwork> Networks { get; init; } = [.. ordering.Networks.Select(network => models.Network(network))];

    /// <summary>
    /// The ordering's own images. A user's ordering gets them through the
    /// image cross-reference endpoints, as <c>user</c>/<c>ordering</c>/<see cref="LocalID"/>.
    /// </summary>
    [Required]
    public Images Images { get; init; } = ordering.GetImages().ToDto();

    /// <summary>
    /// The ordering's groups in viewing order, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<OrderingGroup>? Groups { get; init; } = includeGroups
        ? [.. ordering.Seasons.Select(season => new OrderingGroup(season, MetadataOrderingService.ListGroupEntries(ordering, season)))]
        : null;

    /// <summary>
    /// When the ordering was made.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; init; } = ordering.CreatedAt.ToUniversalTime();

    /// <summary>
    /// When the ordering last changed.
    /// </summary>
    [Required]
    public DateTime LastUpdatedAt { get; init; } = ordering.LastUpdatedAt.ToUniversalTime();

    /// <summary>
    /// One group of an ordering, with its episodes in order.
    /// </summary>
    /// <param name="season">The group, as a season of its ordering.</param>
    /// <param name="entries">The entries it lists.</param>
    public class OrderingGroup(ISeason season, IReadOnlyList<OrderingGroupEntry> entries)
    {
        /// <summary>
        /// The group's full ID, as a season.
        /// </summary>
        [Required]
        public string ID { get; init; } = season.ID.ToString();

        /// <summary>
        /// The group's name.
        /// </summary>
        [Required]
        public string Name { get; init; } = season.Title;

        /// <summary>
        /// The group's number in the ordering: the season's own number for the
        /// default ordering, <c>0</c> for the special group of an ordering
        /// stored on this server and the others' place among themselves from
        /// 1.
        /// </summary>
        [Required]
        public int SeasonNumber { get; init; } = season.SeasonNumber;

        /// <summary>
        /// Whether the group holds the ordering's specials.
        /// </summary>
        [Required]
        public bool IsSpecial { get; init; } = season.IsSpecial;

        /// <summary>
        /// The group's images: a season's own for the default ordering, and
        /// the group's own for any other. A group of a user's ordering gets
        /// them through the image cross-reference endpoints, as
        /// <c>user</c>/<c>season</c> and the local part of its <see cref="ID"/>.
        /// </summary>
        [Required]
        public Images Images { get; init; } = season.GetImages().ToDto();

        /// <summary>
        /// The group's episodes, in order. A regular group also lists the
        /// placed specials airing in it, flagged <see cref="OrderingEpisode.IsSpecial"/>
        /// with their special group numbers, so a group's IDs round-trip
        /// through an update.
        /// </summary>
        [Required]
        public IReadOnlyList<OrderingEpisode> Episodes { get; init; } = [.. entries.Select(entry => new OrderingEpisode(entry))];
    }

    /// <summary>
    /// One episode's place in a group.
    /// </summary>
    /// <param name="entry">The entry.</param>
    public class OrderingEpisode(OrderingGroupEntry entry)
    {
        /// <summary>
        /// The episode's full ID.
        /// </summary>
        [Required]
        public string ID { get; init; } = entry.EpisodeID.ToString();

        /// <summary>
        /// The Shoko episode ID, for an episode of a Shoko series.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? ShokoEpisodeID { get; init; } = entry.EpisodeID.Source == MetadataSource.Shoko && entry.EpisodeID.TryGetNumericID<int>(out var localID)
            ? localID
            : null;

        /// <summary>
        /// The episode's number: among the group's home episodes, from 1, or
        /// its number in the special group for a special. The default
        /// ordering keeps each episode's own number.
        /// </summary>
        [Required]
        public int EpisodeNumber { get; init; } = entry.EpisodeNumber;

        /// <summary>
        /// Whether the episode is a special here: it is in the special group,
        /// or it is a placed special airing in this regular group.
        /// </summary>
        [Required]
        public bool IsSpecial { get; init; } = entry.IsSpecial;

        /// <summary>
        /// The season of the regular episode a placed special airs before.
        /// Set only on a placed special's entry in the special group.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? AirsBeforeSeasonNumber { get; init; } = entry.AirsBeforeSeasonNumber;

        /// <summary>
        /// The number of the regular episode a placed special airs before.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? AirsBeforeEpisodeNumber { get; init; } = entry.AirsBeforeEpisodeNumber;

        /// <summary>
        /// The season a placed special airs after, when no episode of it
        /// follows the special.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? AirsAfterSeasonNumber { get; init; } = entry.AirsAfterSeasonNumber;

        /// <summary>
        /// The full ID of the regular episode a placed special airs right
        /// after, in any group.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? AirsAfterEpisodeID { get; init; } = entry.AirsAfterEpisodeID?.ToString();

        /// <summary>
        /// The full ID of the regular episode a placed special airs right
        /// before, in any group.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? AirsBeforeEpisodeID { get; init; } = entry.AirsBeforeEpisodeID?.ToString();
    }

    /// <summary>
    /// The bodies the ordering endpoints take.
    /// </summary>
    public static class Input
    {
        /// <summary>
        /// A user's ordering of a Shoko series, to make or to replace whole.
        /// </summary>
        public class OrderingBody
        {
            /// <summary>
            /// The ordering's name.
            /// </summary>
            [Required, MinLength(1)]
            public string Name { get; set; } = string.Empty;

            /// <summary>
            /// What the ordering is about, if anything.
            /// </summary>
            public string? Description { get; set; }

            /// <summary>
            /// The ordering's groups, in viewing order.
            /// </summary>
            [Required]
            public List<GroupBody> Groups { get; set; } = [];
        }

        /// <summary>
        /// One group of a user's ordering.
        /// </summary>
        public class GroupBody
        {
            /// <summary>
            /// The full ID of one of the ordering's groups to keep, on an
            /// update. Left out, the group is new.
            /// </summary>
            public string? ID { get; set; }

            /// <summary>
            /// The group's name. Left out or empty, the group has no title of
            /// its own and is named by its generic season name, such as
            /// <c>Season 2</c> or <c>Specials</c>.
            /// </summary>
            public string? Name { get; set; }

            /// <summary>
            /// What the group is about, if anything.
            /// </summary>
            public string? Description { get; set; }

            /// <summary>
            /// Whether the group holds the ordering's specials. It is numbered
            /// season <c>0</c>, and at most one group of an ordering may be.
            /// </summary>
            public bool IsSpecial { get; set; }

            /// <summary>
            /// The Shoko episode IDs of the group's episodes, in order. Each
            /// must be an episode of the series; an episode may be in more
            /// than one group. An episode in the special group and a regular
            /// one is a placed special, airing where the regular group lists it.
            /// </summary>
            [Required]
            public List<int> EpisodeIDs { get; set; } = [];
        }

        /// <summary>
        /// The ordering to choose for a series.
        /// </summary>
        public class SetPreferredOrderingBody
        {
            /// <summary>
            /// The full ID of one of the series' orderings, a user's ordering's
            /// local ID, or <c>null</c> or <c>default</c> for its default one.
            /// </summary>
            public string? OrderingID { get; set; }
        }
    }
}
