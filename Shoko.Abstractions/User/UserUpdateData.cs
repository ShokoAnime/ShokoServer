
using System.Collections.Generic;
using System.IO;
using Shoko.Abstractions.Metadata.Anidb;

namespace Shoko.Abstractions.User;

/// <summary>
///   Represents an update to a user.
/// </summary>
public class UserUpdateData
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
    ///   Indicates that the user avatar image has been set or unset in this update.
    /// </summary>
    public bool HasSetAvatarImage { get; private set; }

    /// <summary>
    ///   Indicates this update contains an avatar image to-be set.
    /// </summary>
    public bool HasAvatarImage { get => AvatarImage is not null || AvatarImageAsStream is not null; }

    private byte[]? _avatarImage;

    private Stream? _avatarImageAsStream;

    /// <summary>
    ///   The avatar image to set, as an array of bytes. Can be encoded as a
    ///   Base64 Data URL instead of a raw byte array. Set either this or
    ///   <see cref="AvatarImageAsStream"/> to <c>null</c> to unset the avatar
    ///   image.
    /// </summary>
    public byte[]? AvatarImage
    {
        get => _avatarImage;
        set
        {
            HasSetAvatarImage = true;
            _avatarImage = value;
            _avatarImageAsStream = null;
        }
    }

    /// <summary>
    ///   The avatar image to set, as a stream of bytes. Set this or 
    ///   <see cref="AvatarImage"/> to <c>null</c> to unset the avatar image.
    /// </summary>
    public Stream? AvatarImageAsStream
    {
        get => _avatarImageAsStream;
        set
        {
            HasSetAvatarImage = true;
            _avatarImage = null;
            _avatarImageAsStream = value;
        }
    }

    /// <summary>
    ///   The user's new restricted tags.
    /// </summary>
    public List<IAnidbTag>? RestrictedTags { get; set; }

    /// <summary>
    ///   Initializes a new instance of the <see cref="UserUpdateData"/> class.
    /// </summary>
    public UserUpdateData() { }

    /// <summary>
    ///   Initializes a new instance of the <see cref="UserUpdateData"/> class.
    /// </summary>
    /// <param name="user">The user.</param>
    public UserUpdateData(IUser user)
    {
        Username = user.Username;
        IsAdmin = user.IsAdmin;
        IsAnidbUser = user.IsAnidbUser;
        _avatarImageAsStream = user.PortraitImage?.GetStream();
    }
}
