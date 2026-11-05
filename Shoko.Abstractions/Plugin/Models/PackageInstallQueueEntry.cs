using System;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
///   A package install waiting in, or running at the head of, the package
///   manager's install queue.
/// </summary>
public sealed class PackageInstallQueueEntry
{
    /// <summary>
    ///   The entry's ID, a UUIDv5 of the package ID, version and runtime of
    ///   every step of its plan. The same plan always gets the same ID, so
    ///   asking for it while it is queued or running joins this entry, and
    ///   queuing it again after it finished gives the same ID to a new run.
    /// </summary>
    public required Guid ID { get; init; }

    /// <summary>
    ///   The package asked for. Its plan's dependencies are installed with it.
    /// </summary>
    public required PackageInfo Package { get; init; }

    /// <summary>
    ///   The package being installed right now, the one asked for or one of
    ///   its dependencies, or <c>null</c> while the entry waits.
    /// </summary>
    public PackageInfo? CurrentPackage { get; init; }

    /// <summary>
    ///   When the install was first asked for.
    /// </summary>
    public required DateTime QueuedAt { get; init; }

    /// <summary>
    ///   When the install started, or <c>null</c> while it waits.
    /// </summary>
    public DateTime? StartedAt { get; init; }

    /// <summary>
    ///   How many callers are waiting for this install right now. Queuing it
    ///   without waiting does not count, and a caller whose wait was cancelled
    ///   stops counting.
    /// </summary>
    public required int Waiting { get; init; }

    /// <summary>
    ///   Who queued the install, which it runs as, or <c>null</c> for the
    ///   system, such as an automatic upgrade. Callers joining it later do not
    ///   change it.
    /// </summary>
    public required ApiToken? Actor { get; init; }
}
