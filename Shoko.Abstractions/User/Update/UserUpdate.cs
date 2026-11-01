using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Anidb;

namespace Shoko.Abstractions.User.Update;

/// <summary>
///   Represents an update to a user.
/// </summary>
public class UserUpdate
{
    /// <summary>
    ///   The user's new username. Will cause the service to throw if set to an
    ///   empty string or if another user with the same name already exists.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    ///   The user's new password. Can be set to an empty string to set an empty
    ///   password, which is different from <c>null</c> which is not set.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    ///   Promote the user to or demote the user from to administrator status.
    ///   At least 1 administrator is required at all times. You cannot demote
    ///   the last administrator to a non-administrator.
    /// </summary>
    public bool? IsAdmin { get; set; }

    /// <summary>
    ///   Promote the user to or demote the user from to AniDB user status. Only
    ///   a single AniDB user is allowed at all times, so promoting one user
    ///   will demote another if set.
    /// </summary>
    public bool? IsAnidbUser { get; set; }

    /// <summary>
    ///   The user's new restricted tags.
    /// </summary>
    public List<IAnidbTag>? RestrictedTags { get; set; }

    /// <summary>
    ///   Initializes a new instance of the <see cref="UserUpdate"/> class.
    /// </summary>
    public UserUpdate() { }

    /// <summary>
    ///   Initializes a new instance of the <see cref="UserUpdate"/> class.
    /// </summary>
    /// <param name="user">The user.</param>
    public UserUpdate(IUser user)
    {
        Username = user.Username;
        IsAdmin = user.IsAdmin;
        IsAnidbUser = user.IsAnidbUser;
    }
}
