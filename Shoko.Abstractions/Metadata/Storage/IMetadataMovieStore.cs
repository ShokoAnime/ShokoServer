using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   Keeps every plugin source's movies, so a provider does not have to.
/// </summary>
/// <remarks>
///   A stored movie reads back whole, with what the other stores hold for
///   it. Every source but the core's own can be written; the core keeps the
///   movies of <c>shoko</c>, <c>user</c>, <c>generated</c> and <c>anidb</c>
///   itself. A write raises the movie events on
///   <see cref="IMetadataService"/>.
/// </remarks>
public interface IMetadataMovieStore
{
    #region Reading

    /// <summary>
    ///   Looks up a movie.
    /// </summary>
    /// <param name="id">The movie, e.g. <c>anilist://movie/199</c>.</param>
    /// <returns>The movie, or <c>null</c> when it is not stored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
    IMovie? GetMovie(MetadataGuid id);

    /// <summary>
    ///   Every movie a source has stored.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The movies, in no particular order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    IReadOnlyList<IMovie> GetAllMovies(MetadataSource source);

    #endregion

    #region Writing

    /// <summary>
    ///   Stores a movie whole, replacing what was stored for it, its titles,
    ///   overviews and content ratings included.
    /// </summary>
    /// <remarks>
    ///   The collection it names is queued for a fetch through the source's
    ///   collection provider when it is not stored or is due, the movie is
    ///   linked and the provider's <c>collection</c> kind is on.
    /// </remarks>
    /// <param name="movie">The movie.</param>
    /// <returns><c>1</c> when the movie was added or changed, else <c>0</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="movie"/> is or holds <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   The ID does not name a movie or is on a source the core keeps
    ///   itself, the collection is not a collection on the movie's source,
    ///   or a code is too long.
    /// </exception>
    int SaveMovie(MetadataMovieData movie);

    /// <summary>
    ///   Removes a movie with its titles, overviews, content ratings, image
    ///   links, tags, studios, networks, cast, crew, relations and
    ///   suggestions. The creators, characters, studios and networks it named
    ///   stay.
    /// </summary>
    /// <param name="id">The movie.</param>
    /// <returns><c>1</c> when the movie was removed, else <c>0</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   The ID does not name a movie, or is on a source the core keeps itself.
    /// </exception>
    int RemoveMovie(MetadataGuid id);

    #endregion
}
