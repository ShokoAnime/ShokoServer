using System;

namespace Shoko.Abstractions.Plugin.Events;

/// <summary>
///   Dispatched when a repository sync fails.
/// </summary>
public class RepositorySyncFailedEventArgs : RepositorySyncEventArgs
{
    /// <summary>
    ///   Error message describing the failure.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    ///   Original exception if available.
    /// </summary>
    public required Exception? Exception { get; init; }

    /// <summary>
    ///   When the repository sync operation failed.
    /// </summary>
    public required DateTime FailedAt { get; init; }
}
