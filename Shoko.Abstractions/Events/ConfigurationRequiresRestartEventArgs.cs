using System;

namespace Shoko.Abstractions.Events;

/// <summary>
/// Dispatched when a configuration that requires a restart is saved, and when
/// it is no longer needed because all properties across all configurations that
/// caused the need for a restart are changed back to their original values.
/// </summary>
public class ConfigurationRequiresRestartEventArgs : EventArgs
{
    /// <summary>
    /// Indicates that a restart is currently required.
    /// </summary>
    public required bool RequiresRestart { get; init; }
}
