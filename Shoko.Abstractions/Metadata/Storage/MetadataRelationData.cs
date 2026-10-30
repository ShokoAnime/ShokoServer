using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   An authored relation from one entry to another on the same source.
/// </summary>
public sealed record MetadataRelationData
{
    /// <summary>
    ///   The related entry, which may be of another kind.
    /// </summary>
    public required MetadataGuid RelatedID { get; init; }

    /// <summary>
    ///   What the related entry is to this one.
    /// </summary>
    public required RelationType RelationType { get; init; }
}
