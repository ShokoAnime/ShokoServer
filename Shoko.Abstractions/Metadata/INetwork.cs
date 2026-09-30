using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   A source's network, such as a TMDB network: a broadcaster or a
///   streaming service as the source lists it, read through
///   <see cref="ISeries.Networks"/>. The airing schedule's own channels are
///   <see cref="Airing.IAiringChannel"/> instead.
/// </summary>
public interface INetwork : IMetadata, IWithImages, IWithPrimaryImage
{
    /// <summary>
    ///   The main name of the network.
    /// </summary>
    string Name { get; }
}
