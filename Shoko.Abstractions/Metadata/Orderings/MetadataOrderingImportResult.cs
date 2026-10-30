using System;
using System.Collections.Generic;
using System.Linq;

namespace Shoko.Abstractions.Metadata.Orderings;

/// <summary>
///   What an ordering import did, or why it could not read the payload.
/// </summary>
public sealed record MetadataOrderingImportResult
{
    /// <summary>
    ///   Why the payload could not be read. When there are any, nothing was
    ///   written.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    ///   Whether the payload was read.
    /// </summary>
    public bool Succeeded => Errors.Count is 0;

    /// <summary>
    ///   Whether this was a dry run, which wrote nothing.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    ///   What was done with each ordering of the payload, in its order.
    /// </summary>
    public IReadOnlyList<MetadataOrderingImportEntry> Orderings { get; init; } = [];

    /// <summary>
    ///   How many orderings were made.
    /// </summary>
    public int CreatedCount => Orderings.Count(entry => entry.Outcome is MetadataOrderingImportOutcome.Created);

    /// <summary>
    ///   How many orderings were replaced.
    /// </summary>
    public int ReplacedCount => Orderings.Count(entry => entry.Outcome is MetadataOrderingImportOutcome.Replaced);

    /// <summary>
    ///   How many orderings were skipped.
    /// </summary>
    public int SkippedCount => Orderings.Count(entry => entry.Outcome is MetadataOrderingImportOutcome.Skipped);

    /// <summary>
    ///   How many images were restored from the payload.
    /// </summary>
    public int ImagesFromPayload => CountImages(MetadataOrderingImageImportStatus.FromPayload);

    /// <summary>
    ///   How many images were restored from their remote source.
    /// </summary>
    public int ImagesFromUrl => CountImages(MetadataOrderingImageImportStatus.FromUrl);

    /// <summary>
    ///   How many images wait on a queued download.
    /// </summary>
    public int ImagesPending => CountImages(MetadataOrderingImageImportStatus.Pending);

    /// <summary>
    ///   How many images could not be restored.
    /// </summary>
    public int ImagesFailed => CountImages(MetadataOrderingImageImportStatus.Failed);

    /// <summary>
    ///   How many images of every ordering have a status.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>The count.</returns>
    private int CountImages(MetadataOrderingImageImportStatus status)
        => Orderings.Sum(entry => entry.Images.Count(image => image.Status == status));
}
