using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Orderings;

/// <summary>
///   What an ordering export wrote.
/// </summary>
public sealed record MetadataOrderingExportResult
{
    /// <summary>
    ///   The file written: <see cref="MetadataOrderingContainer.Json"/> or
    ///   <see cref="MetadataOrderingContainer.Zip"/>.
    /// </summary>
    public required MetadataOrderingContainer Container { get; init; }

    /// <summary>
    ///   The file's media type, <c>application/json</c> or
    ///   <c>application/zip</c>.
    /// </summary>
    public string ContentType => Container is MetadataOrderingContainer.Zip ? "application/zip" : "application/json";

    /// <summary>
    ///   The file's extension, with its dot.
    /// </summary>
    public string FileExtension => Container is MetadataOrderingContainer.Zip ? ".zip" : ".json";

    /// <summary>
    ///   How many orderings were written.
    /// </summary>
    public int OrderingCount { get; init; }

    /// <summary>
    ///   How many images were written, over every ordering and group.
    /// </summary>
    public int ImageCount { get; init; }

    /// <summary>
    ///   How many of those images carry their file.
    /// </summary>
    public int EmbeddedImageCount { get; init; }

    /// <summary>
    ///   What was left out and why, such as an episode with no AniDB episode
    ///   or an image with no remote URL.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
///   An ordering export held in memory.
/// </summary>
/// <param name="Content">The file's bytes.</param>
/// <param name="Result">What was written.</param>
public sealed record MetadataOrderingExportFile(byte[] Content, MetadataOrderingExportResult Result);
