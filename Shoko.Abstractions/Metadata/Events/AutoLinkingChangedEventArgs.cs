using System;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
///   Dispatched when a source is told to leave a series alone, or to stop
///   leaving it alone.
/// </summary>
public class AutoLinkingChangedEventArgs : EventArgs
{
    /// <summary>
    ///   The Shoko series.
    /// </summary>
    public required IShokoSeries Series { get; init; }

    /// <summary>
    ///   The source that was told.
    /// </summary>
    public required MetadataSource Source { get; init; }

    /// <summary>
    ///   Whether the source may no longer link the series on its own.
    /// </summary>
    public required bool IsDisabled { get; init; }

    /// <summary>
    ///   The API token of whoever made the change, or <c>null</c> when the
    ///   system did it. Stamped when the event is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
