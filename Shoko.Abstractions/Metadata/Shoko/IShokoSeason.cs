using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;

namespace Shoko.Abstractions.Metadata.Shoko;

/// <summary>
/// Fake "season" for the Shoko series.
/// </summary>
public interface IShokoSeason : ISeason, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    ///   The ID of the Shoko series this belongs to, the same ID
    ///   <see cref="ISeason.SeriesID"/> holds as text.
    /// </summary>
    int ShokoSeriesID { get; }

    MetadataGuid ISeason.SeriesID { get => new(MetadataSource.Shoko, MetadataEntityType.Series, ShokoSeriesID.ToString()); }

    /// <summary>
    /// Get the Shoko series info for the "season," if available.
    /// </summary>
    new IShokoSeries Series { get; }

    /// <summary>
    /// All episodes for the Shoko series for the fake "season."
    /// </summary>
    new IReadOnlyList<IShokoEpisode> Episodes { get; }

    /// <summary>
    /// All seasons linked to the fake Shoko "season."
    /// </summary>
    IReadOnlyList<ISeason> LinkedSeasons { get; }

    /// <summary>
    /// All movies linked to the episodes of the fake Shoko "season."
    /// </summary>
    IReadOnlyList<IMovie> LinkedMovies { get; }

    /// <summary>
    ///   Looks up the seasons of other sources the season's episodes are
    ///   linked into, worked out from their episode links.
    /// </summary>
    /// <param name="source">
    ///   The source to look up, TMDB or a plugin's, or <c>null</c> for every
    ///   source.
    /// </param>
    /// <returns>One link per season reached, or an empty list when there are none.</returns>
    IReadOnlyList<IMetadataSeasonCrossReference> GetMetadataSeasonCrossReferences(MetadataSource? source = null);

    /// <summary>
    ///   Looks up the episode-level links of the season's episodes.
    /// </summary>
    /// <param name="source">
    ///   The source to look up, TMDB or a plugin's, or <c>null</c> for every
    ///   source.
    /// </param>
    /// <returns>The links, by episode, or an empty list when there are none.</returns>
    IReadOnlyList<IMetadataEpisodeCrossReference> GetMetadataEpisodeCrossReferences(MetadataSource? source = null);

    /// <summary>
    ///   Looks up the film links held for the season's episodes: the anime is
    ///   the film, kept against the episode standing for it.
    /// </summary>
    /// <param name="source">
    ///   The source to look up, TMDB or a plugin's, or <c>null</c> for every
    ///   source.
    /// </param>
    /// <returns>The links, by episode, or an empty list when there are none.</returns>
    IReadOnlyList<IMetadataMovieCrossReference> GetMetadataMovieCrossReferences(MetadataSource? source = null);
}
