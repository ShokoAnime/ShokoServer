using System;

namespace Shoko.Abstractions.User.Enums;

/// <summary>
///   The reason the user data for the group and user is being saved.
/// </summary>
[Flags]
public enum GroupUserDataSaveReason
{
    /// <summary>
    /// The user data is being saved for no specific reason.
    /// </summary>
    None = 0,

    /// <summary>
    /// The user data is being imported from another source.
    /// </summary>
    Import = 1 << 0,

    /// <summary>
    /// The user data is being saved because the user stats for the group and user changed.
    /// </summary>
    GroupStats = 1 << 1,

    /// <summary>
    /// The user data is being saved because the user updated their unique tags for the group.
    /// </summary>
    UserTags = 1 << 2,
}
