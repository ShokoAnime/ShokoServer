using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            return Repo.Instance.AniDB_Character.GetByID(character.CharID);
        }


        public static List<Trakt_Episode> GetEpisodes(this Trakt_Season season) => Repo.Instance.Trakt_Episode
            .GetByShowIDAndSeason(season.Trakt_ShowID, season.Season);

        public static List<Trakt_Season> GetSeasons(this Trakt_Show show)
        {
            return Repo.Instance.Trakt_Season.GetByShowID(show.Trakt_ShowID);
        }



        public static AniDB_Seiyuu GetSeiyuu(this AniDB_Character character)
        {
            List<AniDB_Character_Seiyuu> charSeiyuus =
                Repo.Instance.AniDB_Character_Seiyuu.GetByCharID(character.CharID);

            if (charSeiyuus.Count > 0)
            {
                // just use the first creator
                return Repo.Instance.AniDB_Seiyuu.GetByID(charSeiyuus[0].SeiyuuID);
            }

            return null;
        }

        public static void CreateAnimeEpisode(this AniDB_Episode episode, int animeSeriesID)
        {
            SVR_AnimeEpisode existingEp;
            // check if there is an existing episode for this EpisodeID
            using (var upd = Repo.Instance.AnimeEpisode.BeginAddOrUpdate(() => Repo.Instance.AnimeEpisode.GetByAniDBEpisodeID(episode.EpisodeID)))
            {
                upd.Entity.Populate_RA(episode);
                upd.Entity.AnimeSeriesID = animeSeriesID;
                existingEp = upd.Commit();
            }
            
            // We might have removed our AnimeEpisode_User records when wiping out AnimeEpisodes, recreate them if there's watched files
            foreach (var videoLocal in existingEp.GetVideoLocals())
            {
                var videoLocalUsers = Repo.Instance.VideoLocal_User.GetByVideoLocalID(videoLocal.VideoLocalID);
                if (videoLocalUsers.Count > 0)
                {
                    foreach (var videoLocalUser in videoLocalUsers)
                    {
                        using (var upd = Repo.Instance.AnimeEpisode_User.BeginAddOrUpdate(() =>
                            Repo.Instance.AnimeEpisode_User.GetByUserIDAndEpisodeID(
                                videoLocalUser.JMMUserID,
                                existingEp.AnimeEpisodeID)))
                        {
                            upd.Entity.JMMUserID = videoLocalUser.JMMUserID;
                            upd.Entity.WatchedDate = videoLocalUser.WatchedDate;
                            upd.Entity.PlayedCount = videoLocalUser.WatchedDate.HasValue ? 1 : 0;
                            upd.Entity.WatchedCount = videoLocalUser.WatchedDate.HasValue ? 1 : 0;
                            upd.Entity.AnimeSeriesID = animeSeriesID;
                            upd.Entity.AnimeEpisodeID = existingEp.AnimeEpisodeID;
                            upd.Commit();
                        }
                    }
                }
                else
                {
                    // these will probably never exist, but if they do, cover our bases
                    Repo.Instance.AnimeEpisode_User.Touch(() => Repo.Instance.AnimeEpisode_User.GetByEpisodeID(existingEp.AnimeEpisodeID));
                }
            }
        }

        public static MovieDB_Movie GetMovieDB_Movie(this CrossRef_AniDB_Other cross)
        {
            if (cross.CrossRefType != (int) CrossRefType.MovieDB)
                return null;
            return Repo.Instance.MovieDb_Movie.GetByOnlineID(int.Parse(cross.CrossRefID));
        }

        public static Trakt_Show GetByTraktShow(this CrossRef_AniDB_TraktV2 cross)
        {
            return Repo.Instance.Trakt_Show.GetByTraktSlug(cross.TraktID);
        }

        public static TvDB_Series GetTvDBSeries(this CrossRef_AniDB_TvDB cross)
        {
            return Repo.Instance.TvDB_Series.GetByTvDBID(cross.TvDBID);
        }

        public static AniDB_Episode GetEpisode(this CrossRef_File_Episode cross)
        {
            return Repo.Instance.AniDB_Episode.GetByEpisodeID(cross.EpisodeID);
        }

        public static VideoLocal_User GetVideoLocalUserRecord(this CrossRef_File_Episode cross, int userID)
        {
            SVR_VideoLocal vid = Repo.Instance.VideoLocal.GetByHash(cross.Hash);
            if (vid != null)
            {
                VideoLocal_User vidUser = vid.GetUserRecord(userID);
                if (vidUser != null) return vidUser;
            }

            return null;
        }

        public static SVR_ImportFolder GetImportFolder1(this DuplicateFile duplicatefile) => Repo.Instance.ImportFolder
            .GetByID(duplicatefile.ImportFolderIDFile1);

        public static string GetFullServerPath1(this DuplicateFile duplicatefile) => Path.Combine(
            duplicatefile.GetImportFolder1().ImportFolderLocation, duplicatefile.FilePathFile1);

        public static SVR_ImportFolder GetImportFolder2(this DuplicateFile duplicatefile) => Repo.Instance.ImportFolder
            .GetByID(duplicatefile.ImportFolderIDFile2);

        public static string GetFullServerPath2(this DuplicateFile duplicatefile) => Path.Combine(
            duplicatefile.GetImportFolder2().ImportFolderLocation, duplicatefile.FilePathFile2);

        public static SVR_AniDB_File GetAniDBFile(this DuplicateFile duplicatefile) => Repo.Instance.AniDB_File.GetByHash(
            duplicatefile.Hash);

        public static string GetEnglishTitle(this AniDB_Episode ep)
        {
            return Repo.Instance.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(ep.EpisodeID, "EN").FirstOrDefault()
                ?.Title;
        }
    }
}
