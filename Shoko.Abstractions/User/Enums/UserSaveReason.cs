using System;

namespace Shoko.Abstractions.User.Enums;

/// <summary>
///   What changed on a user, for the user added and updated events. The
///   avatar is not among them: it is an image, and the image manager's cross
///   reference events announce its changes, however they are made.
/// </summary>
[Flags]
public enum UserSaveReason
{
    /// <summary>
    ///   Nothing in particular, as for a removed user.
    /// </summary>
    None = 0,

    /// <summary>
    ///   The username changed.
    /// </summary>
    Username = 1 << 0,

    /// <summary>
    ///   The password changed, or was cleared.
    /// </summary>
    Password = 1 << 1,

    /// <summary>
    ///   The user became an administrator, or stopped being one.
    /// </summary>
    IsAdmin = 1 << 2,

    /// <summary>
    ///   The user became the AniDB user, or stopped being it, including when
    ///   another user took the role over.
    /// </summary>
    IsAnidbUser = 1 << 3,

    /// <summary>
    ///   The tags hidden from the user changed.
    /// </summary>
    RestrictedTags = 1 << 4,
}
