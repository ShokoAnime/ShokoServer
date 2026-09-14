using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata.Anilist.CrossReferences;
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
    public int ID { get; init; }

    /// <summary>
    /// Anilist Anime ID.
    /// </summary>
    public int AnimeID { get; init; }

    /// <summary>
    /// Anilist airing schedule ID, if the episode has a schedule entry.
    /// </summary>
    public int? ScheduleID { get; init; }

    /// <summary>
    /// Episode number.
    /// </summary>
    public int EpisodeNumber { get; init; }

    /// <summary>
    /// Episode runtime in minutes.
    /// </summary>
    public TimeSpan Runtime { get; init; }

    /// <summary>
    /// When the episode aired, in UTC.
    /// </summary>
    public DateTime? AiredAt { get; init; }

    /// <summary>
    /// When the local metadata was first created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the local metadata was last updated.
    /// </summary>
    public DateTime LastUpdatedAt { get; init; }

    /// <summary>
    /// Episode cross-references.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<CrossReference>? CrossReferences { get; init; }

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
        public int AnidbAnimeID { get; init; }

        /// <summary>
        /// AniDB Episode ID.
        /// </summary>
        public int AnidbEpisodeID { get; init; }

        /// <summary>
        /// Anilist Anime ID.
        /// </summary>
        public int AnilistAnimeID { get; init; }

        /// <summary>
        /// Anilist Episode ID.
        /// </summary>
        public int AnilistEpisodeID { get; init; }

        /// <summary>
        /// Episode number in Anilist.
        /// </summary>
        public int EpisodeNumber { get; init; }

        /// <summary>
        /// The index to order the cross-reference if multiple references
        /// exists for the same anidb or anilist episode.
        /// </summary>
        public int Index { get; init; }

        /// <summary>
        /// The match rating.
        /// </summary>
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
    }
}
