namespace Shoko.Abstractions.Metadata.Orderings;

/// <summary>
///   How to read an ordering export into local orderings.
/// </summary>
public sealed record MetadataOrderingImportOptions
{
    /// <summary>
    ///   What to do when the series already has a local ordering with the
    ///   same name.
    /// </summary>
    public MetadataOrderingConflictMode ConflictMode { get; init; } = MetadataOrderingConflictMode.Skip;

    /// <summary>
    ///   Where to restore each image from.
    /// </summary>
    public MetadataOrderingImageImportMode ImageMode { get; init; } = MetadataOrderingImageImportMode.PayloadFirst;

    /// <summary>
    ///   Whether to refuse an image file whose SHA-256 is not the one the
    ///   payload gives for it.
    /// </summary>
    public bool VerifyHashes { get; init; } = true;

    /// <summary>
    ///   Whether to choose each imported ordering that was its series'
    ///   chosen ordering where it was exported.
    /// </summary>
    public bool ApplyPreferred { get; init; }

    /// <summary>
    ///   Whether to only work out and report what would be done, writing
    ///   nothing.
    /// </summary>
    public bool DryRun { get; init; }
}
