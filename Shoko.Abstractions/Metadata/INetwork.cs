using System;
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

    /// <summary>
    ///   The country the network originates from, as the source gives it,
    ///   usually an ISO 3166-1 code such as <c>JP</c>, or <c>null</c> when
    ///   the source does not say.
    /// </summary>
    string? CountryOfOrigin { get; }

    /// <summary>
    ///   When the core last asked the source to refresh the network, found or
    ///   not, in UTC. Set by the core alone; <c>null</c> when it
    ///   never did.
    /// </summary>
    DateTime? LastRefreshedAt { get; }
}
