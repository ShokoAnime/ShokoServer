using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Storage;

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
