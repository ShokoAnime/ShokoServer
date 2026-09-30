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
}

/// <summary>
///   A studio's part in one entry.
/// </summary>
public sealed record MetadataEntryStudioData
{
    /// <summary>
    ///   The studio.
    /// </summary>
    public required MetadataGuid StudioID { get; init; }

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
}
