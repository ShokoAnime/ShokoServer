using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.API.v3.Models.Common;

#nullable enable
namespace Shoko.Server.API.v3.Models.Airing;

/// <summary>
/// One cached AniDB anime of a yearly season, with what a season view card
/// shows: its details and its next airing.
/// </summary>
public class SeasonAnime
{
    /// <summary>
    /// The AniDB anime ID.
    /// </summary>
    [Required]
    public required int ID { get; init; }

    /// <summary>
    /// The ID of the Shoko series, or <c>null</c> when the anime is not in the
    /// collection.
    /// </summary>
    public required int? ShokoID { get; init; }

    /// <summary>
    /// The anime type.
    /// </summary>
    [Required]
    public required AnimeType Type { get; init; }

    /// <summary>
    /// The preferred title: the series' when the anime is in the collection,
    /// else the anime's.
    /// </summary>
    [Required]
    public required string Title { get; init; }

    /// <summary>
    /// The poster: the series' primary image when it has one, else the
    /// anime's.
    /// </summary>
    public required Image? Poster { get; init; }

    /// <summary>
    /// The preferred overview: the series' when the anime is in the
    /// collection, else the anime's.
    /// </summary>
    public required string? Overview { get; init; }

    /// <summary>
    /// When the anime starts airing, in the <c>yyyy</c>, <c>yyyy-MM</c> or
    /// <c>yyyy-MM-dd</c> format.
    /// </summary>
    public required PartialDateOnly? AirDate { get; init; }

    /// <summary>
    /// When the anime stops airing, in the same format, or <c>null</c> while
    /// it is unknown.
    /// </summary>
    public required PartialDateOnly? EndDate { get; init; }

    /// <summary>
    /// How many regular episodes the anime has, when known.
    /// </summary>
    public required int? EpisodeCount { get; init; }

    /// <summary>
    /// Whether the anime is restricted.
    /// </summary>
    [Required]
    public required bool Restricted { get; init; }

    /// <summary>
    /// The animation studios of AniDB and the linked sources in the season
    /// detail source order, by source rank, AniDB first unless the order
    /// places it, each named once. With none, the untyped studios of the
    /// highest ranked source fill in.
    /// </summary>
    [Required]
    public required List<Studio> Studios { get; init; }

    /// <summary>
    /// What the anime was adapted from.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public required SourceMaterial SourceMaterial { get; init; }

    /// <summary>
    /// The genres most sources agree on, at most <see cref="TagLimit"/>,
    /// without spoilers: AniDB's genre tags by weight and the genres of the
    /// linked sources in the season detail source order, merged by name.
    /// Ties go by source rank, AniDB first unless the order places it.
    /// </summary>
    [Required]
    public required List<Tag> Tags { get; init; }

    /// <summary>
    /// How many local files the anime's series has, each counted once;
    /// <c>0</c> when it is not in the collection.
    /// </summary>
    [Required]
    public required int VideoCount { get; init; }

    /// <summary>
    /// The yearly season the anime starts in, by the rule the seasons are
    /// listed with, or <c>null</c> when it has no dates to go by.
    /// </summary>
    public required SeasonWithYear? StartSeason { get; init; }

    /// <summary>
    /// The usual length of a regular episode, or <c>null</c> when no regular
    /// episode has a known length.
    /// </summary>
    public required TimeSpan? EpisodeDuration { get; init; }

    /// <summary>
    /// Whether the anime has a next airing, has finished, or neither is known.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public required NextAiringStatus AiringStatus { get; init; }

    /// <summary>
    /// The next airing: the earliest episode still to come, on the airing the
    /// server prefers for it, or a date-only entry for an episode known only
    /// by its AniDB air date.
    /// </summary>
    public required EpisodeAiring? NextAiring { get; init; }

    /// <summary>
    /// The other upcoming airings of the <see cref="NextAiring"/>'s episode,
    /// the next one on each other channel, in airing order.
    /// </summary>
    [Required]
    public required List<EpisodeAiring> OtherAirings { get; init; }

    /// <summary>
    /// How many genre tags an anime is sent with.
    /// </summary>
    public const int TagLimit = 5;

    #region Nested Types

    /// <summary>
    /// Whether an anime has a next airing.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum NextAiringStatus
    {
        /// <summary>
        /// Neither a next airing nor an end is known.
        /// </summary>
        Unknown,

        /// <summary>
        /// The anime has a next airing.
        /// </summary>
        Upcoming,

        /// <summary>
        /// The anime has no next airing and its end date has passed.
        /// </summary>
        Finished,
    }

    /// <summary>
    /// An animation studio of the anime.
    /// </summary>
    public class Studio
    {
        /// <summary>
        /// The source the studio was taken from.
        /// </summary>
        [Required]
        public required MetadataSource Source { get; init; }

        /// <summary>
        /// The source's own ID for the studio.
        /// </summary>
        [Required]
        public required string ID { get; init; }

        /// <summary>
        /// The name.
        /// </summary>
        [Required]
        public required string Name { get; init; }
    }

    /// <summary>
    /// A genre tag of the anime.
    /// </summary>
    public class Tag
    {
        /// <summary>
        /// The source the tag was taken from.
        /// </summary>
        [Required]
        public required MetadataSource Source { get; init; }

        /// <summary>
        /// The source's own ID for the tag.
        /// </summary>
        [Required]
        public required string ID { get; init; }

        /// <summary>
        /// The name.
        /// </summary>
        [Required]
        public required string Name { get; init; }
    }

    #endregion
}
