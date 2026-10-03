using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   A studio to store.
/// </summary>
public sealed record MetadataStudioData
{
    /// <summary>
    ///   The studio: its source, the <c>studio</c> kind and the source's own
    ///   ID for it, e.g. <c>anilist://studio/7</c>.
    /// </summary>
    public required MetadataGuid ID { get; init; }

    /// <summary>
    ///   The studio's name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   The studio's name in its original script, when it differs.
    /// </summary>
    public string? OriginalName { get; init; }

    /// <summary>
    ///   The country the studio originates from, usually an ISO 3166-1 code
    ///   such as <c>JP</c>, if the source says.
    /// </summary>
    public string? CountryOfOrigin { get; init; }

    /// <summary>
    ///   The source's resource ID of the studio's default image of each type,
    ///   which becomes its pinned default and is always the first downloaded
    ///   within the type's limit. <c>null</c> leaves the stored defaults as
    ///   they are, and an empty map clears them. A type the studio has no
    ///   images of is ignored.
    /// </summary>
    public IReadOnlyDictionary<ImageEntityType, string>? DefaultImageResourceIDs { get; init; }
}

/// <summary>
///   A studio's part in one entry. A studio not stored yet is kept as a stub.
/// </summary>
public sealed record MetadataEntryStudioData
{
    /// <summary>
    ///   The studio.
    /// </summary>
    public required MetadataGuid StudioID { get; init; }

    /// <summary>
    ///   The studio's name, kept on the stub the core makes when the studio
    ///   is not stored yet.
    /// </summary>
    public string? StudioName { get; init; }

    /// <summary>
    ///   What the studio did for the entry.
    /// </summary>
    public StudioType Type { get; init; } = StudioType.Animation;
}

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

/// <summary>
///   A network one entry aired or streamed on. A network not stored yet is
///   kept as a stub.
/// </summary>
public sealed record MetadataEntryNetworkData
{
    /// <summary>
    ///   The network.
    /// </summary>
    public required MetadataGuid NetworkID { get; init; }

    /// <summary>
    ///   The network's name, kept on the stub the core makes when the network
    ///   is not stored yet.
    /// </summary>
    public string? NetworkName { get; init; }
}
