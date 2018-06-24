using System.Collections.Generic;
using System.IO;
using System.Linq;
using NHibernate;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Extensions
{
    public static class ModelDatabase
    {


        public static AniDB_Character GetCharacter(this AniDB_Anime_Character character)
        {
            return Repo.AniDB_Character.GetByID(character.CharID);
        }


        public static List<Trakt_Episode> GetEpisodes(this Trakt_Season season) => Repo.Trakt_Episode
            .GetByShowIDAndSeason(season.Trakt_ShowID, season.Season);

        public static List<Trakt_Season> GetSeasons(this Trakt_Show show)
        {
            return Repo.Trakt_Season.GetByShowID(show.Trakt_ShowID);
        }



        public static AniDB_Seiyuu GetSeiyuu(this AniDB_Character character)
        {
            List<AniDB_Character_Seiyuu> charSeiyuus =
                Repo.AniDB_Character_Seiyuu.GetByCharID(character.CharID);

            if (charSeiyuus.Count > 0)
            {
                // just use the first creator
                return Repo.AniDB_Seiyuu.GetByID(charSeiyuus[0].SeiyuuID);
            }

            return null;
        }

        public static void CreateAnimeEpisode(this AniDB_Episode episode, int animeSeriesID)
        {
            // check if there is an existing episode for this EpisodeID

            if (Repo.AnimeEpisode.GetByAniDBEpisodeID(episode.EpisodeID).Count==0)
            {
                using (var upd = Repo.AnimeEpisode.BeginAdd())
                {
                    upd.Entity.Populate_RA(episode);
                    upd.Entity.AnimeSeriesID = animeSeriesID;
                    upd.Commit();
                }
            }
            else
            {
                if (existingEp.AnimeSeriesID != animeSeriesID) existingEp.AnimeSeriesID = animeSeriesID;
                existingEp.PlexContract = null;
                RepoFactory.AnimeEpisode.Save(existingEp);
                foreach (var episodeUser in RepoFactory.AnimeEpisode_User.GetByEpisodeID(existingEp.AnimeEpisodeID))
                    RepoFactory.AnimeEpisode_User.SaveWithOpenTransaction(session, episodeUser);
            }
        }



        public static MovieDB_Movie GetMovieDB_Movie(this CrossRef_AniDB_Other cross)
        {
            if (cross.CrossRefType != (int) CrossRefType.MovieDB)
                return null;
            return RepoFactory.MovieDb_Movie.GetByOnlineID(session, int.Parse(cross.CrossRefID));
        }

        public static Trakt_Show GetByTraktShow(this CrossRef_AniDB_TraktV2 cross)
        {
            return Repo.Trakt_Show.GetByTraktSlug(cross.TraktID);
        }

        public static TvDB_Series GetTvDBSeries(this CrossRef_AniDB_TvDB cross)
        {
            return Repo.TvDB_Series.GetByTvDBID(cross.TvDBID);
        }

        public static AniDB_Episode GetEpisode(this CrossRef_File_Episode cross)
        {
            return Repo.AniDB_Episode.GetByEpisodeID(cross.EpisodeID);
        }

        public static VideoLocal_User GetVideoLocalUserRecord(this CrossRef_File_Episode cross, int userID)
        {
            SVR_VideoLocal vid = Repo.VideoLocal.GetByHash(cross.Hash);
            if (vid != null)
            {
                VideoLocal_User vidUser = vid.GetUserRecord(userID);
                if (vidUser != null) return vidUser;
            }

            return null;
        }

        public static SVR_ImportFolder GetImportFolder1(this DuplicateFile duplicatefile) => Repo.ImportFolder
            .GetByID(duplicatefile.ImportFolderIDFile1);

        public static string GetFullServerPath1(this DuplicateFile duplicatefile) => Path.Combine(
            duplicatefile.GetImportFolder1().ImportFolderLocation, duplicatefile.FilePathFile1);

        public static SVR_ImportFolder GetImportFolder2(this DuplicateFile duplicatefile) => Repo.ImportFolder
            .GetByID(duplicatefile.ImportFolderIDFile2);

        public static string GetFullServerPath2(this DuplicateFile duplicatefile) => Path.Combine(
            duplicatefile.GetImportFolder2().ImportFolderLocation, duplicatefile.FilePathFile2);

        public static SVR_AniDB_File GetAniDBFile(this DuplicateFile duplicatefile) => Repo.AniDB_File.GetByHash(
            duplicatefile.Hash);

        public static string GetEnglishTitle(this AniDB_Episode ep)
        {
            return RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(ep.EpisodeID, "EN").FirstOrDefault()
                ?.Title;
        }
    }
}
