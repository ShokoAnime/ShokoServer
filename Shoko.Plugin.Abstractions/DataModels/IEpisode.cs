using System;

#nullable enable
namespace Shoko.Plugin.Abstractions.DataModels;

public interface IEpisode : IWithTitles, IMetadata<int>
{
    /// <summary>
    /// The AniDB Anime ID.
    /// </summary>
    int SeriesID { get; }

    /// <summary>
    /// The episode type.
    /// </summary>
    EpisodeType Type { get; }

    /// <summary>
    /// The episode number.
    /// </summary>
    int EpisodeNumber { get; }

    /// <summary>
    /// The season number, if applicable.
    /// </summary>
    int? SeasonNumber { get; }

    /// <summary>
    /// The runtime of the episode, as a time span.
    /// </summary>
    TimeSpan Runtime { get; }

    /// <summary>
    /// When the episode aired or will air, if it's known.
    /// </summary>
    DateTime? AirDate { get; }

    /// <summary>
    /// Get the series info for the episode, if available.
    /// </summary>
    ISeries? SeriesInfo { get; }

    #region To-be-removed

    /// <summary>
    /// The AniDB Episode ID.
    /// </summary>
    [Obsolete("Use ID instead.")]
    int EpisodeID { get; }

    /// <summary>
    /// The AniDB Anime ID.
    /// </summary>
    [Obsolete("Use ShowID instead.")]
    int AnimeID { get; }

    /// <summary>
    /// The runtime of the episode, in seconds.
    /// </summary>
    [Obsolete("Use Runtime instead.")]
    int Duration { get; }

    /// <summary>
    /// 
    /// </summary>
    [Obsolete("Use EpisodeNumber instead.")]
    int Number { get; }

    #endregion
}
