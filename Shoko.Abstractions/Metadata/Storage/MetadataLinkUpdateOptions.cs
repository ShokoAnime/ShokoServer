using System;
using Shoko.Abstractions.Metadata.CrossReferences;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   How to write links, for the writes that take it.
/// </summary>
public sealed record MetadataLinkUpdateOptions
{
    /// <summary>
    ///   Whether an entry's links that the write leaves out are removed.
    ///   Off by default, so a write only touches what it names.
    /// </summary>
    public bool ReplaceExisting { get; init; }

    /// <summary>
    ///   The provider the written links came from, recorded on them as
    ///   <see cref="IMetadataCrossReference.WrittenBy"/>. Left out, they are
    ///   recorded as written by nobody in particular.
    /// </summary>
    public Guid? WrittenBy { get; init; }
}
