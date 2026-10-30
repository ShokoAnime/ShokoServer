using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   A network to store.
/// </summary>
public sealed record MetadataNetworkData
{
    /// <summary>
    ///   The network: its source, the <c>network</c> kind and the source's
    ///   own ID for it, e.g. <c>anilist://network/3</c>.
    /// </summary>
    public required MetadataGuid ID { get; init; }

    /// <summary>
    ///   The network's name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   The country the network originates from, usually an ISO 3166-1
    ///   code such as <c>JP</c>, if the source says.
    /// </summary>
    public string? CountryOfOrigin { get; init; }

    /// <summary>
    ///   The source's resource ID of the network's default image of each type,
    ///   which becomes its pinned default and is always the first downloaded
    ///   within the type's limit. <c>null</c> leaves the stored defaults as
    ///   they are, and an empty map clears them. A type the network has no
    ///   images of is ignored.
    /// </summary>
    public IReadOnlyDictionary<ImageEntityType, string>? DefaultImageResourceIDs { get; init; }
}
