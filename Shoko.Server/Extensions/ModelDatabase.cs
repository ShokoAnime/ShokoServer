using System.Collections.Generic;
using NHibernate;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Server.Extensions;

public static class ModelDatabase
{
    public static AniDB_Character GetCharacter(this AniDB_Anime_Character character)
        => RepoFactory.AniDB_Character.GetByCharID(character.CharID);

    public static AniDB_Seiyuu GetSeiyuu(this AniDB_Character character)
        => RepoFactory.AniDB_Character_Seiyuu.GetByCharID(character.CharID) is { Count: > 0 } characterVAs
            ? RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(characterVAs[0].SeiyuuID)
            : null;

    public static Trakt_Show GetByTraktShow(this CrossRef_AniDB_TraktV2 cross, ISession session)
        => RepoFactory.Trakt_Show.GetByTraktSlug(session, cross.TraktID);

    public static List<Trakt_Episode> GetTraktEpisodes(this Trakt_Season season)
        => RepoFactory.Trakt_Episode.GetByShowIDAndSeason(season.Trakt_ShowID, season.Season);

    public static List<Trakt_Season> GetTraktSeasons(this Trakt_Show show)
        => RepoFactory.Trakt_Season.GetByShowID(show.Trakt_ShowID);

    public static TvDB_Series GetTvDBSeries(this CrossRef_AniDB_TvDB cross)
        => RepoFactory.TvDB_Series.GetByTvDBID(cross.TvDBID);
}
