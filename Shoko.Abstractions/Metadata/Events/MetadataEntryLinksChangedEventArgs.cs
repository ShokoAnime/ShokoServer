using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
///   Dispatched once per write that changed which studios or networks one or
///   more entries are linked to, whatever source they are on.
/// </summary>
public class MetadataEntryLinksChangedEventArgs : EventArgs
{
    /// <summary>
    ///   The entries whose studios or networks changed, never empty.
    /// </summary>
    public required IReadOnlyList<MetadataGuid> EntityIDs { get; init; }
}
