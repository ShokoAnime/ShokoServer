
namespace Shoko.Server.API.v1.Models;

public enum CL_ImageEntityType
{
    None = 0, // The lack of a type. Should generally not be used, except as a null/default check
    AniDB_Cover = 1, // use AnimeID
    AniDB_Character = 2, // use CharID
    AniDB_Creator = 3, // use CreatorID
    MovieDB_FanArt = 8,
    MovieDB_Poster = 9,
    Character = 14,
    Staff = 15,
    UserAvatar = 17,
}
