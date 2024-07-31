using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.DataModels.Shoko;

namespace Shoko.Plugin.Abstractions.DataModels;

public interface IEpisode : IWithTitles, IWithDescriptions, IWithImages, IMetadata<int>
{
    /// <summary>
    /// The series id.
    /// </summary>
    int SeriesID { get; }

    /// <summary>
    /// The shoko episode ID, if we have any.
    /// </summary>
    IReadOnlyList<int> ShokoEpisodeIDs { get; }

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
    /// All shoko episodes linked to this episode.
    /// </summary>
    IReadOnlyList<IShokoEpisode> ShokoEpisodes { get; }

    /// <summary>
    /// All cross-references linked to the episode.
    /// </summary>
    IReadOnlyList<IVideoCrossReference> CrossReferences { get; }

    /// <summary>
    /// Get all videos linked to the episode, if any.
    /// </summary>
    IReadOnlyList<IVideo> VideoList { get; }
}
