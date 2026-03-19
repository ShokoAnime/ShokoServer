using System;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Abstractions.Plugin.Events;

/// <summary>
///   Event dispatched when the installation of a plugin has completed.
/// </summary>
public class PackageInstallationCompletedEventArgs : PackageInstallationEventArgs
{
    /// <summary>
    ///   The newly installed plugin definition.
    /// </summary>
    public required LocalPluginInfo Plugin { get; init; }

    /// <summary>
    ///   When the installation of the plugin completed.
    /// </summary>
    public required DateTime CompletedAt { get; init; }
}
