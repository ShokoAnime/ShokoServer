using System;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Plugin.Events;

/// <summary>
/// Base class for package installation event arguments.
/// </summary>
public class PackageInstallationEventArgs : EventArgs
{
    /// <summary>
    ///   The package.
    /// </summary>
    public required PackageInfo Package { get; init; }

    /// <summary>
    ///   The ID of the install queue entry installing the package, the one
    ///   asked for or one of its dependencies.
    /// </summary>
    public required Guid QueueEntryID { get; init; }

    /// <summary>
    /// When the package installation process started.
    /// </summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>
    ///   Who queued the install, or <c>null</c> for the system.
    /// </summary>
    public required ApiToken? Actor { get; init; }
}
