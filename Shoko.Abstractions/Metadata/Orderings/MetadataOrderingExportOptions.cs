using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Orderings;

/// <summary>
///   Which orderings to write into an ordering export, and how.
/// </summary>
/// <remarks>
///   With neither <see cref="OrderingIDs"/> nor <see cref="SeriesIDs"/>
///   given, every local ordering on the server is written. Any ordering can
///   be named, the default and global ones too, so one can be forked into a
///   local ordering elsewhere.
/// </remarks>
public sealed record MetadataOrderingExportOptions
{
    /// <summary>
    ///   The orderings to write, of any kind.
    /// </summary>
    public IReadOnlyList<MetadataGuid>? OrderingIDs { get; init; }

    /// <summary>
    ///   The series, of any source, whose local orderings to write, and their
    ///   global ones with <see cref="IncludeGlobalOrderings"/>.
    /// </summary>
    public IReadOnlyList<MetadataGuid>? SeriesIDs { get; init; }

    /// <summary>
    ///   Whether <see cref="SeriesIDs"/> also takes the series' stored global
    ///   orderings. The default ordering is only written when named.
    /// </summary>
    public bool IncludeGlobalOrderings { get; init; }

    /// <summary>
    ///   How to carry the images of the orderings and their groups.
    /// </summary>
    public MetadataOrderingImageExportMode ImageMode { get; init; } = MetadataOrderingImageExportMode.UrlOnly;

    /// <summary>
    ///   The file to write.
    /// </summary>
    public MetadataOrderingContainer Container { get; init; } = MetadataOrderingContainer.Auto;

    /// <summary>
    ///   Whether to record which ordering each series uses.
    /// </summary>
    public bool IncludePreferred { get; init; } = true;
}
