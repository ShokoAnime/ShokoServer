using System;
using Shoko.Abstractions.Plugin.Models;

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
    /// When the package installation process started.
    /// </summary>
    public required DateTime StartedAt { get; init; }
}
