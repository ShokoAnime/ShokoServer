using System;
using System.Collections.Generic;
using Shoko.Models.Enums;

namespace Shoko.Server.Filters.Interfaces;

public interface IFilterable
{
    /// <summary>
    /// Name
    /// </summary>
    string Name { get; init; }

    /// <summary>
    /// Sorting Name
    /// </summary>
    string SortingName { get; init; }

    /// <summary>
    /// The number of series in a group
    /// </summary>
    int SeriesCount { get; init; }
    
    /// <summary>
    /// Number of Missing Episodes
    /// </summary>
    int MissingEpisodes { get; init; }
    
    /// <summary>
    /// Number of Missing Episodes from Groups that you have
    /// </summary>
    int MissingEpisodesCollecting { get; init; }

    /// <summary>
    /// All of the tags
    /// </summary>
    IReadOnlySet<string> Tags { get; init; }

    /// <summary>
    /// All of the custom tags
    /// </summary>
    IReadOnlySet<string> CustomTags { get; init; }
    
    /// <summary>
    /// The years this aired in
    /// </summary>
    IReadOnlySet<int> Years { get; init; }
    
    /// <summary>
    /// The seasons this aired in
    /// </summary>
    IReadOnlySet<(int year, AnimeSeason season)> Seasons { get; init; }

    /// <summary>
    /// Has at least one TvDB Link
    /// </summary>
    bool HasTvDBLink { get; init; }

    /// <summary>
    /// Missing at least one TvDB Link
    /// </summary>
    bool HasMissingTvDbLink { get; init; }
    
    /// <summary>
    /// Has at least one TMDb Link
    /// </summary>
    bool HasTMDbLink { get; init; }
    
    /// <summary>
    /// Missing at least one TMDb Link
    /// </summary>
    bool HasMissingTMDbLink { get; init; }
    
    /// <summary>
    /// Has at least one Trakt Link
    /// </summary>
    bool HasTraktLink { get; init; }
    
    /// <summary>
    /// Missing at least one Trakt Link
    /// </summary>
    bool HasMissingTraktLink { get; init; }
    
    /// <summary>
    /// Has Finished airing
    /// </summary>
    bool IsFinished { get; init; }
    
    /// <summary>
    /// First Air Date
    /// </summary>
    DateTime? AirDate { get; init; }
    
    /// <summary>
    /// Latest Air Date
    /// </summary>
    DateTime? LastAirDate { get; init; }
    
    /// <summary>
    /// When it was first added to the collection
    /// </summary>
    DateTime AddedDate { get; init; }
    
    /// <summary>
    /// When it was most recently added to the collection
    /// </summary>
    DateTime LastAddedDate { get; init; }

    /// <summary>
    /// Highest Episode Count
    /// </summary>
    int EpisodeCount { get; init; }

    /// <summary>
    /// Total Episode Count
    /// </summary>
    int TotalEpisodeCount { get; init; }
    
    /// <summary>
    /// Lowest AniDB Rating (0-10)
    /// </summary>
    decimal LowestAniDBRating { get; init; }
    
    /// <summary>
    /// Highest AniDB Rating (0-10)
    /// </summary>
    decimal HighestAniDBRating { get; init; }

    /// <summary>
    /// Average AniDB Rating (0-10)
    /// </summary>
    decimal AverageAniDBRating { get; init; }

    /// <summary>
    /// The sources that the video came from, such as TV, Web, DVD, Blu-ray, etc.
    /// </summary>
    IReadOnlySet<string> VideoSources { get; init; }
    
    /// <summary>
    /// The sources that the video came from, such as TV, Web, DVD, Blu-ray, etc. (only sources that are in every file)
    /// </summary>
    IReadOnlySet<string> SharedVideoSources { get; init; }

    /// <summary>
    /// The anime types (movie, series, ova, etc)
    /// </summary>
    IReadOnlySet<string> AnimeTypes { get; init; }

    /// <summary>
    /// Audio Languages
    /// </summary>
    IReadOnlySet<string> AudioLanguages { get; init; }
    
    /// <summary>
    /// Audio Languages (only languages that are in every file)
    /// </summary>
    IReadOnlySet<string> SharedAudioLanguages { get; init; }
    
    /// <summary>
    /// Subtitle Languages
    /// </summary>
    IReadOnlySet<string> SubtitleLanguages { get; init; }
    
    /// <summary>
    /// Subtitle Languages (only languages that are in every file)
    /// </summary>
    IReadOnlySet<string> SharedSubtitleLanguages { get; init; }
}
