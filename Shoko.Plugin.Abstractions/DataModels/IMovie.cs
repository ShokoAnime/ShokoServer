using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.DataModels.Shoko;

namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Movie metadata.
/// </summary>
public interface IMovie : IWithTitles, IWithDescriptions, IWithImages, IMetadata<int>
{
    /// <summary>
    /// The shoko series ID, if we have any.
    /// /// </summary>
    IReadOnlyList<int> ShokoSeriesIDs { get; }

    /// <summary>
    /// The shoko episode ID, if we have any.
    /// /// </summary>
    IReadOnlyList<int> ShokoEpisodeIDs { get; }

    /// <summary>
    /// The first release date of the movie in the country of origin, if it's known.
    /// </summary>
    DateTime? ReleaseDate { get; }

    /// <summary>
    /// Overall user rating for the show, normalized on a scale of 1-10.
    /// </summary>
    double Rating { get; }

    /// <summary>
    /// Default poster for the movie.
    /// </summary>
    IImageMetadata? DefaultPoster { get; }

    /// <summary>
    /// All shoko episodes linked to the movie.
    /// </summary>
    IReadOnlyList<IShokoEpisode> ShokoEpisodes { get; }

    /// <summary>
    /// All shoko series linked to the movie.
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
    /// All cross-references linked to the episode.
    /// </summary>
    IReadOnlyList<IVideoCrossReference> CrossReferences { get; }

    /// <summary>
    /// Get all videos linked to the series, if any.
    /// </summary>
    IReadOnlyList<IVideo> VideoList { get; }
}
