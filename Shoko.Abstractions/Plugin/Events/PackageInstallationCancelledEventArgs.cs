using System;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Plugin.Events;

/// <summary>
///   Dispatched when an install queue entry was cancelled, while it waited or
///   while it ran.
/// </summary>
public class PackageInstallationCancelledEventArgs : EventArgs
{
    /// <summary>
    ///   The queue entry, as it was when it was cancelled. Its
    ///   <see cref="PackageInstallQueueEntry.Actor"/> is who queued it.
    /// </summary>
    public required PackageInstallQueueEntry Entry { get; init; }

    /// <summary>
    ///   When it was cancelled.
    /// </summary>
    public required DateTime CancelledAt { get; init; }

    /// <summary>
    ///   Who cancelled it, or <c>null</c> for the system or a plugin acting
    ///   outside any request.
    /// </summary>
    public required ApiToken? Actor { get; init; }
}
