using System;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.UserData;

/// <summary>
/// Represents user-specific data.
/// </summary>
public interface IUserData
{
    /// <summary>
    /// Gets the ID of the user.
    /// </summary>
    int UserID { get; }

    /// <summary>
    /// Gets the date and time when the user data was last updated.
    /// </summary>
    DateTime LastUpdatedAt { get; }

    /// <summary>
    /// Gets the user associated with this video data.
    /// </summary>
    IUser User { get; }
}
