using System;
using System.Collections.Generic;
using Shoko.Models.Enums;

namespace Shoko.Server.Models.Filters.Interfaces;

public interface IFilterable
{
    /// <summary>
    /// Number of Missing Episodes
    /// </summary>
    int MissingEpisodes { get; }

    /// <summary>
    /// Number of Missing Episodes from Groups that you have
    /// </summary>
    int MissingEpisodesCollecting { get; }

    /// <summary>
    /// All of the tags
    /// </summary>
    IReadOnlySet<string> Tags { get; }

    /// <summary>
    /// All of the custom tags
    /// </summary>
    IReadOnlySet<string> CustomTags { get; }

    /// <summary>
    /// The years this aired in
    /// </summary>
    IReadOnlySet<int> Years { get; }

    /// <summary>
    /// The seasons this aired in
    /// </summary>
    IReadOnlySet<(int year, AnimeSeason season)> Seasons { get; }

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
    /// First Air Date
    /// </summary>
    DateTime? AirDate { get; }

    /// <summary>
    /// Latest Air Date
    /// </summary>
    DateTime? LastAirDate { get; }

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
