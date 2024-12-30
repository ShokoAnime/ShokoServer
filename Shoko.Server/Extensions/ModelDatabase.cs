using System.Collections.Generic;
using System.Linq;
using NHibernate;
using Shoko.Models.Server;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Trakt;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Extensions;

public static class ModelDatabase
{
    public static AniDB_Creator? GetCreator(this AniDB_Character character)
        => RepoFactory.AniDB_Anime_Character_Creator.GetByCharacterID(character.CharacterID) is { Count: > 0 } characterVAs
            ? characterVAs.OrderBy(x => x.AnimeID).ThenBy(x => x.Ordering).First().Creator
            : null;

    public static IReadOnlyList<AniDB_Anime_Character> GetRoles(this AniDB_Character character)
        => RepoFactory.AniDB_Anime_Character.GetByCharacterID(character.CharacterID);

    public static Trakt_Show? GetByTraktShow(this CrossRef_AniDB_TraktV2 cross, ISession session)
        => RepoFactory.Trakt_Show.GetByTraktSlug(session, cross.TraktID);

    public static List<Trakt_Episode> GetTraktEpisodes(this Trakt_Season season)
        => RepoFactory.Trakt_Episode.GetByShowIDAndSeason(season.Trakt_ShowID, season.Season);

    public static List<Trakt_Season> GetTraktSeasons(this Trakt_Show show)
        => RepoFactory.Trakt_Season.GetByShowID(show.Trakt_ShowID);
}
