using System;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Config.Events;

/// <summary>
/// Dispatched when a stored user configuration is deleted, so whoever reads
/// it falls back to its defaults. Not raised for the configurations that go
/// with a deleted user or a plugin uninstalled with its data.
/// </summary>
public class UserConfigurationDeletedEventArgs : EventArgs
{
    /// <summary>
    /// The configuration that was deleted, as it stands after the deletion.
    /// </summary>
    public required UserConfigurationInfo ConfigurationInfo { get; init; }

    /// <summary>
    /// The user the configuration belonged to.
    /// </summary>
    public required IUser User { get; init; }

    /// <summary>
    ///   The API token of whoever deleted the configuration, or <c>null</c>
    ///   when the system did it.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
