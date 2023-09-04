using System;
using System.Collections.Generic;
using Shoko.Models.Enums;

namespace Shoko.Server.Filters;

public class Filterable
{
    /// <summary>
    ///     Name
    /// </summary>
    public string Name { get; init; }
    /// <summary>
    ///     Sorting Name
    /// </summary>
    public string SortingName { get; init; }
    /// <summary>
    ///     The number of series in a group
    /// </summary>
    public int SeriesCount { get; init; }
    /// <summary>
    ///     Number of Missing Episodes
    /// </summary>
    public int MissingEpisodes { get; init; }
    /// <summary>
    ///     Number of Missing Episodes from Groups that you have
    /// </summary>
    public int MissingEpisodesCollecting { get; init; }
    /// <summary>
    ///     All of the tags
    /// </summary>
    public IReadOnlySet<string> Tags { get; init; }
    /// <summary>
    ///     All of the custom tags
    /// </summary>
    public IReadOnlySet<string> CustomTags { get; init; }
    /// <summary>
    ///     The years this aired in
    /// </summary>
    public IReadOnlySet<int> Years { get; init; }
    /// <summary>
    ///     The seasons this aired in
    /// </summary>
    public IReadOnlySet<(int year, AnimeSeason season)> Seasons { get; init; }
    /// <summary>
    ///     Has at least one TvDB Link
    /// </summary>
    public bool HasTvDBLink { get; init; }
    /// <summary>
    ///     Missing at least one TvDB Link
    /// </summary>
    public bool HasMissingTvDbLink { get; init; }
    /// <summary>
    ///     Has at least one TMDb Link
    /// </summary>
    public bool HasTMDbLink { get; init; }
    /// <summary>
    ///     Missing at least one TMDb Link
    /// </summary>
    public bool HasMissingTMDbLink { get; init; }
    /// <summary>
    ///     Has at least one Trakt Link
    /// </summary>
    public bool HasTraktLink { get; init; }
    /// <summary>
    ///     Missing at least one Trakt Link
    /// </summary>
    public bool HasMissingTraktLink { get; init; }
    /// <summary>
    ///     Has Finished airing
    /// </summary>
    public bool IsFinished { get; init; }
    /// <summary>
    ///     First Air Date
    /// </summary>
    public DateTime? AirDate { get; init; }
    /// <summary>
    ///     Latest Air Date
    /// </summary>
    public DateTime? LastAirDate { get; init; }
    /// <summary>
    ///     When it was first added to the collection
    /// </summary>
    public DateTime AddedDate { get; init; }
    /// <summary>
    ///     When it was most recently added to the collection
    /// </summary>
    public DateTime LastAddedDate { get; init; }
    /// <summary>
    ///     Highest Episode Count
    /// </summary>
    public int EpisodeCount { get; init; }
    /// <summary>
    ///     Total Episode Count
    /// </summary>
    public int TotalEpisodeCount { get; init; }
    /// <summary>
    ///     Lowest AniDB Rating (0-10)
    /// </summary>
    public decimal LowestAniDBRating { get; init; }
    /// <summary>
    ///     Highest AniDB Rating (0-10)
    /// </summary>
    public decimal HighestAniDBRating { get; init; }
    /// <summary>
    ///     The sources that the video came from, such as TV, Web, DVD, Blu-ray, etc.
    /// </summary>
    public IReadOnlySet<string> VideoSources { get; init; }
    /// <summary>
    ///     The anime types (movie, series, ova, etc)
    /// </summary>
    public IReadOnlySet<string> AnimeTypes { get; init; }
    /// <summary>
    ///     Audio Languages
    /// </summary>
    public IReadOnlySet<string> AudioLanguages { get; init; }
    /// <summary>
    ///     Subtitle Languages
    /// </summary>
    public IReadOnlySet<string> SubtitleLanguages { get; init; }
}
