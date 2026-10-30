namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   A tag as it applies to one entry.
/// </summary>
public sealed record MetadataEntryTagData
{
    /// <summary>
    ///   The tag.
    /// </summary>
    public required MetadataGuid TagID { get; init; }

    /// <summary>
    ///   How strongly it applies, on the source's own scale.
    /// </summary>
    public int? Weight { get; init; }

    /// <summary>
    ///   Whether it gives something away for this entry in particular.
    /// </summary>
    public bool IsSpoiler { get; init; }
}
