using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   A collection of movies, such as a film series.
/// </summary>
public interface IMovieCollection : ICollection
{
    /// <summary>
    ///   The stored movies in the collection.
    /// </summary>
    IReadOnlyList<IMovie> Movies { get; }
}

/// <summary>
///   A collection of movies, with its movies typed.
/// </summary>
/// <typeparam name="TMovie">The movies' type.</typeparam>
public interface IMovieCollection<out TMovie> : IMovieCollection
    where TMovie : class, IMovie
{
    /// <summary>
    ///   The stored movies in the collection.
    /// </summary>
    new IReadOnlyList<TMovie> Movies { get; }
}
