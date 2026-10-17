using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
///   Dispatched once per write that removed stored entries, whatever source
///   they are on, with every entry it removed.
/// </summary>
public class MetadataEntitiesRemovedEventArgs : EventArgs
{
    /// <summary>
    ///   The entries removed, never empty.
    /// </summary>
    public required IReadOnlyList<MetadataGuid> EntityIDs { get; init; }
}
