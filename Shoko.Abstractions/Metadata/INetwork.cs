using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   A network something airs on, in any registry. A broadcast station, a
///   streaming service or a TMDB network are all the same kind of thing, and
///   the source keeps their keys apart.
/// </summary>
/// <remarks>
///   This is <see cref="IMetadata"/> rather than <see cref="IMetadata{TId}"/>,
///   because TMDB networks are keyed by an integer and airing channels by a
///   <see cref="System.Guid"/>.
/// </remarks>
public interface INetwork : IMetadata, IWithImages, IWithPrimaryImage
{
    /// <summary>
    ///   The main name of the network.
    /// </summary>
    string Name { get; }
}
