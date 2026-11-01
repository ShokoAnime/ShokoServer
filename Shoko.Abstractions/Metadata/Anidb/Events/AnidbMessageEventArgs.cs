using System;

namespace Shoko.Abstractions.Metadata.Anidb.Events;

/// <summary>
///   Dispatched for an AniDB message that arrived or changed.
/// </summary>
public class AnidbMessageEventArgs : EventArgs
{
    /// <summary>
    ///   The message, as it is now.
    /// </summary>
    public required IAnidbMessage Message { get; init; }
}
