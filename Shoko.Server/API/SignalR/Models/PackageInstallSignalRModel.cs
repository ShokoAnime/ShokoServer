using System;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Server.API.SignalR.Models;

/// <summary>
///   SignalR model for a package install being queued, starting, failing or
///   being cancelled.
/// </summary>
/// <param name="queueEntryID">The install queue entry's ID.</param>
/// <param name="package">The package.</param>
/// <param name="occurredAt">When it happened.</param>
/// <param name="reason">Why it failed, for a failure.</param>
public class PackageInstallSignalRModel(Guid queueEntryID, PackageInfo package, DateTime occurredAt, string? reason = null)
{
    /// <summary>
    ///   The ID of the install queue entry installing the package, the one
    ///   asked for or one of its dependencies.
    /// </summary>
    public Guid QueueEntryID { get; } = queueEntryID;

    /// <summary>
    ///   The package's ID.
    /// </summary>
    public Guid PackageID { get; } = package.Manifest.PackageID;

    /// <summary>
    ///   The package's name.
    /// </summary>
    public string Name { get; } = package.Manifest.Name;

    /// <summary>
    ///   The release's version.
    /// </summary>
    public Version Version { get; } = package.Release.Version;

    /// <summary>
    ///   The archive's runtime identifier.
    /// </summary>
    public string RuntimeIdentifier { get; } = package.Archive.RuntimeIdentifier;

    /// <summary>
    ///   When it happened.
    /// </summary>
    public DateTime OccurredAt { get; } = occurredAt;

    /// <summary>
    ///   Why the install failed, for a failure; otherwise <c>null</c>.
    /// </summary>
    public string? Reason { get; } = reason;
}
