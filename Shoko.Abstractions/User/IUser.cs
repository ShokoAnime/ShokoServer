using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.User;

/// <summary>
/// Shoko user.
/// </summary>
public interface IUser : IWithPortraitImage
{
    /// <summary>
    /// Unique ID.
    /// </summary>
    int ID { get; }

    /// <summary>
    /// Username.
    /// </summary>
    string Username { get; }

    /// <summary>
    /// Indicates that the user is an administrator.
    /// </summary>
    bool IsAdmin { get; }

    /// <summary>
    /// Indicates that the user is an AniDB user.
    /// </summary>
    bool IsAnidbUser { get; }

    /// <summary>
    ///   The restricted tags for the user. Any series with any of these tags
    ///   will be hidden from the user in the REST API.
    /// </summary>
    IReadOnlyList<IAnidbTag> RestrictedTags { get; }
}
