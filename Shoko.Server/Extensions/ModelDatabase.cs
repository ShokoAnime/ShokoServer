using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Utilities;

namespace Shoko.Server.Extensions;

public static class ModelDatabase
{
    public static AniDB_Character GetCharacter(this AniDB_Anime_Character character)
    {
        return RepoFactory.AniDB_Character.GetByCharID(character.CharID);
    }

    public static AniDB_Seiyuu GetSeiyuu(this AniDB_Character character)
    {
        var charSeiyuus = RepoFactory.AniDB_Character_Seiyuu.GetByCharID(character.CharID);
        return charSeiyuus.Count > 0 ? RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(charSeiyuus[0].SeiyuuID) : null;
    }

    public static MovieDB_Movie GetMovieDB_Movie(this CrossRef_AniDB_Other cross)
    {
        var databaseFactory = Utils.ServiceContainer.GetRequiredService<DatabaseFactory>();
        using var session = databaseFactory.SessionFactory.OpenSession();
        return cross.CrossRefType != (int)CrossRefType.MovieDB ? null : RepoFactory.MovieDb_Movie.GetByOnlineID(session.Wrap(), int.Parse(cross.CrossRefID));
    }

    public static Trakt_Show GetByTraktShow(this CrossRef_AniDB_TraktV2 cross, ISession session)
    {
        return RepoFactory.Trakt_Show.GetByTraktSlug(session, cross.TraktID);
    }

    public static List<Trakt_Episode> GetTraktEpisodes(this Trakt_Season season)
    {
        return RepoFactory.Trakt_Episode
            .GetByShowIDAndSeason(season.Trakt_ShowID, season.Season);
    }

    public static List<Trakt_Season> GetTraktSeasons(this Trakt_Show show)
    {
        return RepoFactory.Trakt_Season.GetByShowID(show.Trakt_ShowID);
    }

    public static TvDB_Series GetTvDBSeries(this CrossRef_AniDB_TvDB cross)
    {
        return RepoFactory.TvDB_Series.GetByTvDBID(cross.TvDBID);
    }
}
