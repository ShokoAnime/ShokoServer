using Moq;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.User;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached.AniDB;

namespace Shoko.Tests.Infrastructure;

/// <summary>
/// Users with and without restricted tags, and the AniDB anime they are
/// checked against, for the aggregate hub's feeds.
/// </summary>
public static class TestViewers
{
    #region Constants

    /// <summary>
    /// An anime the restricted user may not see.
    /// </summary>
    public const int HiddenAnimeID = 1;

    /// <summary>
    /// An anime every user may see.
    /// </summary>
    public const int VisibleAnimeID = 2;

    /// <summary>
    /// An anime no longer known, not in <see cref="AnimeRepository"/>.
    /// </summary>
    public const int UnknownAnimeID = 3;

    /// <summary>
    /// The connection of <see cref="Restricted"/>.
    /// </summary>
    public const string RestrictedConnection = "restricted";

    /// <summary>
    /// The connection of <see cref="Unrestricted"/>.
    /// </summary>
    public const string UnrestrictedConnection = "unrestricted";

    #endregion

    #region Factories

    /// <summary>
    /// The known anime: <see cref="HiddenAnimeID"/> and <see cref="VisibleAnimeID"/>.
    /// </summary>
    /// <returns>A cache-backed repository holding them.</returns>
    public static AniDB_AnimeRepository AnimeRepository()
        => CachedRepo.Build<AniDB_AnimeRepository, int, AniDB_Anime>(
            anime => anime.AniDB_AnimeID,
            new AniDB_Anime { AniDB_AnimeID = HiddenAnimeID, AnimeID = HiddenAnimeID },
            new AniDB_Anime { AniDB_AnimeID = VisibleAnimeID, AnimeID = VisibleAnimeID }
        );

    /// <summary>
    /// A user with restricted tags, kept from <see cref="HiddenAnimeID"/>.
    /// </summary>
    /// <param name="userID">The user's ID.</param>
    /// <param name="isAdmin">Whether the user is an admin.</param>
    /// <returns>The user.</returns>
    public static IUser Restricted(int userID = 1, bool isAdmin = false)
    {
        var user = new Mock<IUser>();
        user.Setup(u => u.LocalID).Returns(userID);
        user.Setup(u => u.IsAdmin).Returns(isAdmin);
        user.Setup(u => u.RestrictedTags).Returns([new Mock<IAnidbTag>().Object]);
        user.Setup(u => u.IsAllowedToSee(It.IsAny<IAnidbAnime>())).Returns((IAnidbAnime anime) => anime.AnidbID != HiddenAnimeID);
        return user.Object;
    }

    /// <summary>
    /// A user without restricted tags.
    /// </summary>
    /// <returns>The user.</returns>
    public static IUser Unrestricted()
        => new JMMUser { JMMUserID = 2, Username = "unrestricted", HideCategories = string.Empty };

    #endregion
}
