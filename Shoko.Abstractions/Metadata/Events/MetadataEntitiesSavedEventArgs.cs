using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
///   Dispatched once per write that saved stored entries, whatever source
///   they are on, with every entry it wrote, stubs included.
/// </summary>
public class MetadataEntitiesSavedEventArgs : EventArgs
{
    /// <summary>
    ///   The entries saved, never empty.
    /// </summary>
    public required IReadOnlyList<MetadataGuid> EntityIDs { get; init; }
}
