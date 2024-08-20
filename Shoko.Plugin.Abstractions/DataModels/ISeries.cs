using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.DataModels.Shoko;

namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Series metadata.
/// </summary>
public interface ISeries : IWithTitles, IWithDescriptions, IWithImages, IMetadata<int>
{
    /// <summary>
    /// The shoko series ID, if we have any.
    /// /// </summary>
    IReadOnlyList<int> ShokoSeriesIDs { get; }

    /// <summary>
    /// The Anime Type.
    /// </summary>
    AnimeType Type { get; }

    /// <summary>
    /// The first aired date, if known.
    /// </summary>
    /// <value></value>
    DateTime? AirDate { get; }

    /// <summary>
    /// The end date of the series. Null means that it's still airing.
    /// </summary>
    DateTime? EndDate { get; }

    /// <summary>
    /// Overall user rating for the show, normalized on a scale of 1-10.
    /// </summary>
    double Rating { get; }

    /// <summary>
    /// Indicates it's restricted for non-adult viewers. 😉
    /// </summary>
    bool Restricted { get; }

    /// <summary>
    /// Default poster for the series.
    /// </summary>
    IImageMetadata? DefaultPoster { get; }

    /// <summary>
    /// All shoko series linked to this entity.
    /// </summary>
    IReadOnlyList<IShokoSeries> ShokoSeries { get; }

    /// <summary>
    /// Related series.
    /// </summary>
    IReadOnlyList<IRelatedMetadata<ISeries>> RelatedSeries { get; }

    /// <summary>
    /// Related movies.
    /// </summary>
    IReadOnlyList<IRelatedMetadata<IMovie>> RelatedMovies { get; }

    /// <summary>
    /// All cross-references linked to the series.
    /// </summary>
    IReadOnlyList<IVideoCrossReference> CrossReferences { get; }

    /// <summary>
    /// All known episodes for the show.
    /// </summary>
    IReadOnlyList<IEpisode> Episodes { get; }

    /// <summary>
    /// The number of total episodes in the series.
    /// </summary>
    EpisodeCounts EpisodeCounts { get; }

    /// <summary>
    /// Get all videos linked to the series, if any.
    /// </summary>
    IReadOnlyList<IVideo> Videos { get; }
}
