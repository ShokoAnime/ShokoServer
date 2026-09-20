using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata.Anilist.CrossReferences;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models.Anilist;
using Shoko.Server.Models.CrossReference;

#nullable enable
namespace Shoko.Server.API.v3.Models.Anilist;

/// <summary>
/// APIv3 Anilist Episode Data Transfer Object (DTO).
/// </summary>
public class AnilistEpisode
{
    /// <summary>
    /// Anilist Episode ID.
    /// </summary>
    [Required]
    public int ID { get; init; }

    /// <summary>
    /// Anilist Anime ID.
    /// </summary>
    [Required]
    public int AnimeID { get; init; }

    /// <summary>
    /// Anilist airing schedule ID, if the episode has a schedule entry.
    /// </summary>
    public int? ScheduleID { get; init; }

    /// <summary>
    /// Episode number.
    /// </summary>
    [Required]
    public int EpisodeNumber { get; init; }

    /// <summary>
    /// Episode runtime in minutes.
    /// </summary>
    [Required]
    public TimeSpan Runtime { get; init; }

    /// <summary>
    /// When the episode aired, in UTC.
    /// </summary>
    public DateTime? AiredAt { get; init; }

    /// <summary>
    /// When the local metadata was first created.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the local metadata was last updated.
    /// </summary>
    [Required]
    public DateTime LastUpdatedAt { get; init; }

    /// <summary>
    /// Episode cross-references.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<CrossReference>? CrossReferences { get; init; }

    /// <summary>
    /// Anilist episode to file cross-references.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<FileCrossReference>? FileCrossReferences { get; init; }

    public AnilistEpisode(Anilist_Episode episode, IncludeDetails? includeDetails = null)
    {
        var include = includeDetails ?? default;

        ID = episode.AnilistEpisodeID;
        AnimeID = episode.AnilistAnimeID;
        ScheduleID = episode.AnilistScheduleEpisodeID;
        EpisodeNumber = episode.EpisodeNumber;
        Runtime = episode.Runtime;
        AiredAt = episode.AiredAt;
        if (include.HasFlag(IncludeDetails.CrossReferences))
            CrossReferences = episode.CrossReferences
                .Select(xref => new CrossReference(xref))
                .OrderBy(xref => xref.AnidbEpisodeID)
                .ToList();
        if (include.HasFlag(IncludeDetails.FileCrossReferences))
            FileCrossReferences = FileCrossReference.From(episode.FileCrossReferences);
        CreatedAt = episode.CreatedAt.ToUniversalTime();
        LastUpdatedAt = episode.LastUpdatedAt.ToUniversalTime();
    }

    /// <summary>
    /// APIv3 Anilist Episode Cross-Reference Data Transfer Object (DTO).
    /// </summary>
    public class CrossReference
    {
        /// <summary>
        /// AniDB Anime ID.
        /// </summary>
        [Required]
        public int AnidbAnimeID { get; init; }

        /// <summary>
        /// AniDB Episode ID.
        /// </summary>
        [Required]
        public int AnidbEpisodeID { get; init; }

        /// <summary>
        /// Anilist Anime ID.
        /// </summary>
        [Required]
        public int AnilistAnimeID { get; init; }

        /// <summary>
        /// Anilist Episode ID.
        /// </summary>
        [Required]
        public int AnilistEpisodeID { get; init; }

        /// <summary>
        /// Episode number in Anilist.
        /// </summary>
        [Required]
        public int EpisodeNumber { get; init; }

        /// <summary>
        /// The index to order the cross-reference if multiple references
        /// exists for the same anidb or anilist episode.
        /// </summary>
        [Required]
        public int Index { get; init; }

        /// <summary>
        /// The match rating.
        /// </summary>
        [Required]
        public string Rating { get; init; }

        public CrossReference(CrossRef_AniDB_Anilist_Episode xref, int? index = null)
        {
            AnidbAnimeID = xref.AnidbAnimeID;
            AnidbEpisodeID = xref.AnidbEpisodeID;
            AnilistAnimeID = xref.AnilistAnimeID;
            AnilistEpisodeID = xref.AnilistEpisodeID;
            EpisodeNumber = xref.EpisodeNumber;
            Index = index ?? xref.Ordering;
            Rating = xref.MatchRating.ToString();
        }

        public CrossReference(IAnilistEpisodeCrossReference xref, int? index = null)
            : this((CrossRef_AniDB_Anilist_Episode)xref, index) { }
    }

    [Flags]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum IncludeDetails
    {
        None = 0,
        CrossReferences = 1 << 0,
        FileCrossReferences = 1 << 1,
    }
}
