using System;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Events;

/// <summary>
/// Dispatched when video user data is added, updated, or removed.
/// </summary>
public class UserChangedEventArgs : EventArgs
{
    /// <summary>
    /// The user which had their data updated.
    /// </summary>
    public required IUser User { get; init; }
}
