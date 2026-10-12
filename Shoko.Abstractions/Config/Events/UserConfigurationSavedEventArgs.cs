using System;
using System.Collections.Generic;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Config.Events;

/// <summary>
/// Dispatched when a user configuration is saved, by a user, an admin, the
/// server or a plugin.
/// </summary>
public class UserConfigurationSavedEventArgs : EventArgs
{
    /// <summary>
    /// The configuration that was saved, as it stands after the save.
    /// </summary>
    public required UserConfigurationInfo ConfigurationInfo { get; init; }

    /// <summary>
    /// The user the configuration belongs to.
    /// </summary>
    public required IUser User { get; init; }

    /// <summary>
    ///   The JSON paths whose stored values the save changed, added or
    ///   removed. Paths only, never the values, so secrets stay out of it.
    ///   Every stored path when nothing was stored before.
    /// </summary>
    public IReadOnlyList<string> ChangedPaths { get; init; } = [];

    /// <summary>
    ///   The API token of whoever saved the configuration, or <c>null</c> when
    ///   the system did it.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
