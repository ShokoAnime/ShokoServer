using System;
using Shoko.Plugin.Abstractions.DataModels.Shoko;

namespace Shoko.Plugin.Abstractions.DataModels;

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
    IShokoUser User { get; }
}
