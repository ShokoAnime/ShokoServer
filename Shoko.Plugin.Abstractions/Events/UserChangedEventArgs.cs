using System;
using Shoko.Plugin.Abstractions.DataModels.Shoko;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Dispatched when video user data is added, updated, or removed.
/// </summary>
public class UserChangedEventArgs : EventArgs
{
    /// <summary>
    /// The user which had their data updated.
    /// </summary>
    public required IShokoUser User { get; init; }
}
