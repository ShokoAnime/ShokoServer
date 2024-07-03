using System;
using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.DataModels;

public interface IEpisode : IWithTitles, IWithDescriptions, IMetadata<int>
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

    /// <summary>
    /// All episodes linked to this entity.
    /// </summary>
    IReadOnlyList<IEpisode> LinkedEpisodes { get; }

    /// <summary>
    /// All cross-references linked to the episode.
    /// </summary>
    IReadOnlyList<IVideoCrossReference> CrossReferences { get; }

    /// <summary>
    /// Get all videos linked to the episode, if any.
    /// </summary>
    IReadOnlyList<IVideo> VideoList { get; }
}
