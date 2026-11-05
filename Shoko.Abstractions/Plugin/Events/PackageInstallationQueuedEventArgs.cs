using System;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Abstractions.Plugin.Events;

/// <summary>
///   Dispatched when a package install was added to the install queue.
/// </summary>
public class PackageInstallationQueuedEventArgs : EventArgs
{
    /// <summary>
    ///   The queue entry, as it was when it was added.
    /// </summary>
    public required PackageInstallQueueEntry Entry { get; init; }
}
