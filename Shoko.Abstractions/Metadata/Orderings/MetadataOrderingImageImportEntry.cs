using System;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Orderings;

/// <summary>
///   What an ordering import did with one image.
/// </summary>
public sealed record MetadataOrderingImageImportEntry
{
    /// <summary>
    ///   The place of the group the image is for, from 0, or <c>null</c> for
    ///   the ordering itself.
    /// </summary>
    public int? GroupIndex { get; init; }

    /// <summary>
    ///   What the image is for the entry, such as its poster.
    /// </summary>
    public required ImageEntityType ImageType { get; init; }

    /// <summary>
    ///   What was done.
    /// </summary>
    public required MetadataOrderingImageImportStatus Status { get; init; }

    /// <summary>
    ///   The image on this server, when it was restored or linked.
    /// </summary>
    public Guid? ImageID { get; init; }

    /// <summary>
    ///   Why it failed or was not taken from the first place tried, or
    ///   <c>null</c>.
    /// </summary>
    public string? Reason { get; init; }
}
