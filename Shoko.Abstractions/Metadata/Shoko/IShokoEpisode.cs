using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Metadata.Shoko;

/// <summary>
/// Shoko episode metadata.
/// </summary>
/// <remarks>
///   Unlike other episodes, a Shoko episode can have a primary image: the one
///   of a movie it is linked to, or one set on the episode itself. It has no
///   default primary image of its own.
/// </remarks>
public interface IShokoEpisode : IEpisode, IWithPrimaryImage, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    ///   The ID of the Shoko series this belongs to, the same ID
    ///   <see cref="IEpisode.SeriesID"/> holds as text.
    /// </summary>
    int ShokoSeriesID { get; }

    MetadataGuid IEpisode.SeriesID { get => new(MetadataSource.Shoko, MetadataEntityType.Series, ShokoSeriesID.ToString()); }

    /// <summary>
    ///   The Shoko episode ID, the same ID <see cref="IMetadata.ID"/> holds as
    ///   text.
    /// </summary>
    int LocalID { get; }

    /// <summary>
    /// The id of the anidb episode linked to the shoko episode.
    /// </summary>
    int AnidbEpisodeID { get; }

    /// <summary>
    /// Get the shoko series info for the episode, if available.
    /// </summary>
    new IShokoSeries? Series { get; }

    /// <summary>
    /// A direct link to the anidb episode metadata.
    /// </summary>
    IAnidbEpisode AnidbEpisode { get; }

    /// <summary>
    /// All episodes linked to this shoko episode.
    /// </summary>
    IReadOnlyList<IEpisode> LinkedEpisodes { get; }

    /// <summary>
    /// All movies linked to this shoko episode.
    /// </summary>
    IReadOnlyList<IMovie> LinkedMovies { get; }

    /// <summary>
    ///   Looks up the series-level links of the anime the Shoko episode
    ///   belongs to.
    /// </summary>
    /// <param name="source">
    ///   The source to look up, TMDB or a plugin's, or <c>null</c> for every
    ///   source.
    /// </param>
    /// <returns>The links, or an empty list when there are none.</returns>
    IReadOnlyList<IMetadataSeriesCrossReference> GetMetadataSeriesCrossReferences(MetadataSource? source = null);

    /// <summary>
    ///   Looks up the episode-level links of the Shoko episode: it is the same
    ///   work as another source's episode.
    /// </summary>
    /// <param name="source">
    ///   The source to look up, TMDB or a plugin's, or <c>null</c> for every
    ///   source.
    /// </param>
    /// <returns>The links, or an empty list when there are none.</returns>
    IReadOnlyList<IMetadataEpisodeCrossReference> GetMetadataEpisodeCrossReferences(MetadataSource? source = null);

    /// <summary>
    ///   Looks up the film links held for the Shoko episode: the anime is the
    ///   film, and this episode stands for it. A film shows up here and not
    ///   among the series- or episode-level links.
    /// </summary>
    /// <param name="source">
    ///   The source to look up, TMDB or a plugin's, or <c>null</c> for every
    ///   source.
    /// </param>
    /// <returns>The links, or an empty list when there are none.</returns>
    IReadOnlyList<IMetadataMovieCrossReference> GetMetadataMovieCrossReferences(MetadataSource? source = null);

    /// <summary>
    ///   Gets the user-specific data for the Shoko episode and user.
    /// </summary>
    /// <param name="user">
    ///   The user to get the data for.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="user"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   Thrown when the <paramref name="user"/> is not stored in the database.
    /// </exception>
    /// <returns>
    ///   The user-specific data for the Shoko episode and user.
    /// </returns>
    IEpisodeUserData GetUserData(IUser user);
}
