using System.Collections.Generic;
using System.IO;
using System.Linq;
using Force.DeepCloner;
using NHibernate;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Extensions;

public static class ModelDatabase
{
    public static AniDB_Character GetCharacter(this AniDB_Anime_Character character)
    {
        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            return character.GetCharacter(session.Wrap());
        }
    }

    public static AniDB_Character GetCharacter(this AniDB_Anime_Character character, ISessionWrapper session)
    {
        return RepoFactory.AniDB_Character.GetByCharID(session, character.CharID);
    }


    public static List<Trakt_Episode> GetEpisodes(this Trakt_Season season)
    {
        return RepoFactory.Trakt_Episode
            .GetByShowIDAndSeason(season.Trakt_ShowID, season.Season);
    }

    public static List<Trakt_Season> GetSeasons(this Trakt_Show show)
    {
        return RepoFactory.Trakt_Season.GetByShowID(show.Trakt_ShowID);
    }

    public static AniDB_Seiyuu GetSeiyuu(this AniDB_Character character)
    {
        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            return character.GetSeiyuu(session);
        }
    }

    public static AniDB_Seiyuu GetSeiyuu(this AniDB_Character character, ISession session)
    {
        var charSeiyuus =
            RepoFactory.AniDB_Character_Seiyuu.GetByCharID(session, character.CharID);

        if (charSeiyuus.Count > 0)
        {
            // just use the first creator
            return RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(session.Wrap(), charSeiyuus[0].SeiyuuID);
        }

        return null;
    }

    public static void CreateAnimeEpisode(this AniDB_Episode episode, int animeSeriesID)
    {
        // check if there is an existing episode for this EpisodeID
        var existingEp = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(episode.EpisodeID) ??
                         new SVR_AnimeEpisode();

        var old = existingEp.DeepClone();
        existingEp.Populate(episode);
        existingEp.AnimeSeriesID = animeSeriesID;

        if (!old.Equals(existingEp))
            RepoFactory.AnimeEpisode.Save(existingEp);

        // We might have removed our AnimeEpisode_User records when wiping out AnimeEpisodes, recreate them if there's watched files
        var vlUsers = existingEp.GetVideoLocals()
            .SelectMany(a => RepoFactory.VideoLocalUser.GetByVideoLocalID(a.VideoLocalID)).ToList();

        // get the list of unique users
        var users = vlUsers.Select(a => a.JMMUserID).Distinct();

        if (vlUsers.Count > 0)
        {
            // per user. An episode is watched if any file is
            foreach (var uid in users)
            {
                // get the last watched file
                var vlUser = vlUsers.Where(a => a.JMMUserID == uid && a.WatchedDate != null)
                    .MaxBy(a => a.WatchedDate);
                // create or update the record
                var epUser = existingEp.GetUserRecord(uid);
                if (epUser != null) continue;

                epUser = new SVR_AnimeEpisode_User(uid, existingEp.AnimeEpisodeID, animeSeriesID)
                {
                    WatchedDate = vlUser?.WatchedDate,
                    PlayedCount = vlUser != null ? 1 : 0,
                    WatchedCount = vlUser != null ? 1 : 0
                };
                RepoFactory.AnimeEpisode_User.Save(epUser);
            }
        }
        else
        {
            // since these are created with VideoLocal_User,
            // these will probably never exist, but if they do, cover our bases
            RepoFactory.AnimeEpisode_User.Delete(RepoFactory.AnimeEpisode_User.GetByEpisodeID(existingEp.AnimeEpisodeID));
        }
    }

    public static MovieDB_Movie GetMovieDB_Movie(this CrossRef_AniDB_Other cross)
    {
        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            return cross.GetMovieDB_Movie(session.Wrap());
        }
    }

    public static MovieDB_Movie GetMovieDB_Movie(this CrossRef_AniDB_Other cross, ISessionWrapper session)
    {
        if (cross.CrossRefType != (int)CrossRefType.MovieDB)
        {
            return null;
        }

        return RepoFactory.MovieDb_Movie.GetByOnlineID(session, int.Parse(cross.CrossRefID));
    }

    public static Trakt_Show GetByTraktShow(this CrossRef_AniDB_TraktV2 cross)
    {
        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            return cross.GetByTraktShow(session);
        }
    }

    public static Trakt_Show GetByTraktShow(this CrossRef_AniDB_TraktV2 cross, ISession session)
    {
        return RepoFactory.Trakt_Show.GetByTraktSlug(session, cross.TraktID);
    }

    public static TvDB_Series GetTvDBSeries(this CrossRef_AniDB_TvDB cross)
    {
        return RepoFactory.TvDB_Series.GetByTvDBID(cross.TvDBID);
    }

    public static AniDB_Episode GetEpisode(this CrossRef_File_Episode cross)
    {
        return RepoFactory.AniDB_Episode.GetByEpisodeID(cross.EpisodeID);
    }

    public static SVR_VideoLocal_User GetVideoLocalUserRecord(this CrossRef_File_Episode cross, int userID)
    {
        return RepoFactory.VideoLocal.GetByHash(cross.Hash)?.GetUserRecord(userID);
    }

    public static string GetEnglishTitle(this AniDB_Episode ep)
    {
        return RepoFactory.AniDB_Episode_Title
            .GetByEpisodeIDAndLanguage(ep.EpisodeID, Shoko.Plugin.Abstractions.DataModels.TitleLanguage.English)
            .FirstOrDefault()
            ?.Title;
    }
}
