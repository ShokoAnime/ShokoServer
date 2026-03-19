using System;

namespace Shoko.Abstractions.Plugin.Events;

/// <summary>
///   Dispatched when a repository sync is started.
/// </summary>
public class RepositorySyncStartedEventArgs : RepositorySyncEventArgs
{
    /// <summary>
    ///   Gets or sets a value indicating whether the event should be canceled.
    /// </summary>
    public bool Cancel { get; set; }

    /// <summary>
    ///   Get or set the force sync option.
    /// </summary>
    public required bool ForceSync { get; set; }

    private TimeSpan _staleTime;

    /// <summary>
    ///   Get or set the stale time for the current sync operation.
    /// </summary>
    public required TimeSpan StaleTime
    {
        get => _staleTime;
        set => _staleTime = value >= TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(nameof(StaleTime), "Stale time cannot be less than zero.");
    }
}
