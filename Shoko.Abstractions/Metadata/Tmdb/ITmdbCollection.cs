using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
/// A TMDB collection.
/// </summary>
public interface ITmdbCollection : ICollection, IWithImages, IWithCreationDate, IWithUpdateDate { }
