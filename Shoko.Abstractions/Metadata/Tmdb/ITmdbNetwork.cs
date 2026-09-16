using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
/// A TMDB network.
/// </summary>
public interface ITmdbNetwork : INetwork, IMetadata<int>
{
    /// <summary>
    /// Main name of the network on TMDB.
    /// </summary>
    new string Name { get; }

    /// <summary>
    /// The country the network originates from.
    /// </summary>
    string CountryOfOrigin { get; }

    /// <summary>
    /// The shows associated with this network, ordered by TMDB show id.
    /// </summary>
    IReadOnlyList<ITmdbShow> Shows { get; }
}
