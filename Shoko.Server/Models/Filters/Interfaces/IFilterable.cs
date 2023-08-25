using System;
using System.Collections.Generic;

namespace Shoko.Server.Models.Filters.Interfaces;

public interface IFilterable
{
    /// <summary>
    /// Probably will be removed in the future. Custom Tags would handle this better
    /// </summary>
    bool IsFavorite { get; }

    /// <summary>
    /// Number of Missing Episodes
    /// </summary>
    int MissingEpisodes { get; }

    /// <summary>
    /// Number of Missing Episodes from Groups that you have
    /// </summary>
    int MissingEpisodesCollecting { get; }

    /// <summary>
    /// The number of episodes watched
    /// </summary>
    int WatchedEpisodes { get; }

    /// <summary>
    /// The number of episodes that have not been watched
    /// </summary>
    int UnwatchedEpisodes { get; }

    /// <summary>
    /// All of the tags
    /// </summary>
    IReadOnlySet<string> Tags { get; }

    /// <summary>
    /// All of the Custom Tags
    /// </summary>
    IReadOnlySet<string> CustomTags { get; }

    /// <summary>
    /// The years this aired in
    /// </summary>
    IReadOnlySet<int> Years { get; }

    /// <summary>
    /// The seasons this aired in
    /// </summary>
    IReadOnlySet<string> Seasons { get; }

    /// <summary>
    /// Has at least one TvDB Link
    /// </summary>
    bool HasTvDBLink { get; }

    /// <summary>
    /// Missing at least one TvDB Link
    /// </summary>
    bool HasMissingTvDbLink { get; }
    
    /// <summary>
    /// Has at least one TMDb Link
    /// </summary>
    bool HasTMDbLink { get; }

    /// <summary>
    /// Missing at least one TMDb Link
    /// </summary>
    bool HasMissingTMDbLink { get; }
    
    /// <summary>
    /// Has at least one Trakt Link
    /// </summary>
    bool HasTraktLink { get; }

    /// <summary>
    /// Missing at least one Trakt Link
    /// </summary>
    bool HasMissingTraktLink { get; }

    /// <summary>
    /// Has Finished airing
    /// </summary>
    bool IsFinished { get; }

    /// <summary>
    /// Has any user votes
    /// </summary>
    bool HasVotes { get; }

    /// <summary>
    /// Has permanent (after finishing) user votes
    /// </summary>
    bool HasPermanentVotes { get; }

    /// <summary>
    /// First Air Date
    /// </summary>
    DateTime? AirDate { get; }

    /// <summary>
    /// Latest Air Date
    /// </summary>
    DateTime? LastAirDate { get; }

    /// <summary>
    /// First Watched Date
    /// </summary>
    DateTime? WatchedDate { get; }

    /// <summary>
    /// Latest Watched Date
    /// </summary>
    DateTime? LastWatchedDate { get; }

    /// <summary>
    /// When it was first added to the collection
    /// </summary>
    DateTime AddedDate { get; }

    /// <summary>
    /// When it was most recently added to the collection
    /// </summary>
    DateTime LastAddedDate { get; }

    /// <summary>
    /// Highest Episode Count
    /// </summary>
    int EpisodeCount { get; }

    /// <summary>
    /// Total Episode Count
    /// </summary>
    int TotalEpisodeCount { get; }

    /// <summary>
    /// Lowest AniDB Rating (0-10)
    /// </summary>
    decimal LowestAniDBRating { get; }

    /// <summary>
    /// Highest AniDB Rating (0-10)
    /// </summary>
    decimal HighestAniDBRating { get; }

    /// <summary>
    /// Lowest User Rating (0-10)
    /// </summary>
    decimal LowestUserRating { get; }

    /// <summary>
    /// Highest User Rating (0-10)
    /// </summary>
    decimal HighestUserRating { get; }

    /// <summary>
    /// The sources that the video came from, such as TV, Web, DVD, Blu-ray, etc.
    /// </summary>
    IReadOnlySet<string> VideoSources { get; }

    /// <summary>
    /// The anime types (movie, series, ova, etc)
    /// </summary>
    IReadOnlySet<string> AnimeTypes { get; }

    /// <summary>
    /// Audio Languages
    /// </summary>
    IReadOnlySet<string> AudioLanguages { get; }

    /// <summary>
    /// Subtitle Languages
    /// </summary>
    IReadOnlySet<string> SubtitleLanguages { get; }
}
