using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
/// A TMDB collection.
/// </summary>
public interface ITmdbCollection : ICollection, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    /// All movies in the collection.
    /// </summary>
    IReadOnlyList<ITmdbMovie> Movies { get; }
}
