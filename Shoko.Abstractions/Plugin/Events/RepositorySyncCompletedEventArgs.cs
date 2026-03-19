using System;

namespace Shoko.Abstractions.Plugin.Events;

/// <summary>
///   Event dispatched when a repository sync operation completes.
/// </summary>
public sealed class RepositorySyncCompletedEventArgs : RepositorySyncEventArgs
{
    /// <summary>
    ///   When the repository sync operation completed.
    /// </summary>
    public required DateTime CompletedAt { get; init; }
}
