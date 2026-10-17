using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
///   Dispatched once per write that changed the cast or crew of one or more
///   entries, whatever source they are on.
/// </summary>
public class MetadataCreditsChangedEventArgs : EventArgs
{
    /// <summary>
    ///   The entries whose cast or crew changed, never empty.
    /// </summary>
    public required IReadOnlyList<MetadataGuid> EntityIDs { get; init; }
}
