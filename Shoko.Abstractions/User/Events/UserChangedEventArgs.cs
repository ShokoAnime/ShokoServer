using System;
using Shoko.Abstractions.User.Enums;

namespace Shoko.Abstractions.User.Events;

/// <summary>
/// Dispatched when a user is added, updated or removed.
/// </summary>
public class UserChangedEventArgs : EventArgs
{
    /// <summary>
    /// The user which had their data updated.
    /// </summary>
    public required IUser User { get; init; }

    /// <summary>
    ///   What changed on the user. For an added user, the fields it was
    ///   created with; <see cref="UserSaveReason.None"/> for a removed one.
    /// </summary>
    public required UserSaveReason Reason { get; init; }

    /// <summary>
    ///   The API token of whoever added, updated or removed the user, or
    ///   <see langword="null"/> when the system did it. Stamped when the event is
    ///   raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
