using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.User;

/// <summary>
/// Shoko user.
/// </summary>
public interface IUser : IMetadata<int>, IWithPrimaryImage, IWithBackdropImage
{
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

    /// <summary>
    ///   Indicates that the user is allowed to see the specified group based on
    ///   the user's restricted tags.
    /// </summary>
    /// <param name="group">
    ///   The group.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the user is allowed to see the group; otherwise,
    ///   <c>false</c>.
    /// </returns>
    bool IsAllowedToSee(IShokoGroup group);

    /// <summary>
    ///   Indicates that the user is allowed to see the specified series based
    ///   on the user's restricted tags.
    /// </summary>
    /// <param name="series">
    ///   The series.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the user is allowed to see the series; otherwise,
    ///   <c>false</c>.
    /// </returns>
    bool IsAllowedToSee(IShokoSeries series);

    /// <summary>
    ///   Indicates that the user is allowed to see the specified anime based on
    ///   the user's restricted tags.
    /// </summary>
    /// <param name="anime">
    ///   The anime.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the user is allowed to see the anime; otherwise,
    ///   <c>false</c>.
    /// </returns>
    bool IsAllowedToSee(IAnidbAnime anime);
}
