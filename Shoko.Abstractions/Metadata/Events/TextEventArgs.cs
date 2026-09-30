using System;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
///   Dispatched when one stored text is added, updated or removed through the
///   text manager's single-text writes.
/// </summary>
public class TextEventArgs : EventArgs
{
    /// <summary>
    ///   The text as it is after the change, or as it was before it was
    ///   removed.
    /// </summary>
    public required IText Text { get; init; }
}
