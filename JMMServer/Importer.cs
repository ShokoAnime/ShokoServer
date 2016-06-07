using System;
using System.Collections.Generic;
using System.IO;
using JMMFileHelper;
using JMMServer.Commands;
using JMMServer.Commands.AniDB;
using JMMServer.Commands.Azure;
using JMMServer.Entities;
using JMMServer.Providers.MovieDB;
using JMMServer.Providers.MyAnimeList;
using JMMServer.Providers.TraktTV;
using JMMServer.Providers.TvDB;
using JMMServer.Repositories;
using NLog;

namespace JMMServer
{
    public class Importer
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public static void RunImport_IntegrityCheck()
        {
            var repVidLocals = new VideoLocalRepository();
            var repAniFile = new AniDB_FileRepository();
            var repAniEps = new AniDB_EpisodeRepository();
            var repAniAnime = new AniDB_AnimeRepository();


            // files which don't have a valid import folder
            var filesToDelete = repVidLocals.GetVideosWithoutImportFolder();
            foreach (var vl in filesToDelete)
                repVidLocals.Delete(vl.VideoLocalID);


            // files which have not been hashed yet
            // or files which do not have a VideoInfo record
            var filesToHash = repVidLocals.GetVideosWithoutHash();
            var dictFilesToHash = new Dictionary<int, VideoLocal>();
            foreach (var vl in filesToHash)
            {
                dictFilesToHash[vl.VideoLocalID] = vl;
                var cmd = new CommandRequest_HashFile(vl.FullServerPath, false);
                cmd.Save();
            }

            var filesToRehash = repVidLocals.GetVideosWithoutVideoInfo();
            var dictFilesToRehash = new Dictionary<int, VideoLocal>();
            foreach (var vl in filesToHash)
            {
                dictFilesToRehash[vl.VideoLocalID] = vl;
                // don't use if it is in the previous list
                if (!dictFilesToHash.ContainsKey(vl.VideoLocalID))
                {
                    try
                    {
                        var cmd = new CommandRequest_HashFile(vl.FullServerPath, false);
                        cmd.Save();
                    }
                    catch (Exception ex)
                    {
                        var msg = string.Format("Error RunImport_IntegrityCheck XREF: {0} - {1}", vl.ToStringDetailed(),
                            ex);
                        logger.Info(msg);
                    }
                }
            }

            // files which have been hashed, but don't have an associated episode
            var filesWithoutEpisode = repVidLocals.GetVideosWithoutEpisode();
            var dictFilesWithoutEpisode = new Dictionary<int, VideoLocal>();
            foreach (var vl in filesWithoutEpisode)
                dictFilesWithoutEpisode[vl.VideoLocalID] = vl;


            // check that all the episode data is populated
            var filesAll = repVidLocals.GetAll();
            var dictFilesAllExisting = new Dictionary<string, VideoLocal>();
            foreach (var vl in filesAll)
            {
                try
                {
                    dictFilesAllExisting[vl.FullServerPath] = vl;
                }
                catch (Exception ex)
                {
                    var msg = string.Format("Error RunImport_IntegrityCheck XREF: {0} - {1}", vl.ToStringDetailed(), ex);
                    logger.Error(msg);
                    continue;
                }

                // check if it has an episode
                if (dictFilesWithoutEpisode.ContainsKey(vl.VideoLocalID))
                {
                    var cmd = new CommandRequest_ProcessFile(vl.VideoLocalID, false);
                    cmd.Save();
                    continue;
                }

                // if the file is not manually associated, then check for AniDB_File info
                var aniFile = repAniFile.GetByHash(vl.Hash);
                foreach (var xref in vl.EpisodeCrossRefs)
                {
                    if (xref.CrossRefSource != (int)CrossRefSource.AniDB) continue;
                    if (aniFile == null)
                    {
                        var cmd = new CommandRequest_ProcessFile(vl.VideoLocalID, false);
                        cmd.Save();
                    }
                }

                if (aniFile == null) continue;

                // the cross ref is created before the actually episode data is downloaded
                // so lets check for that
                var missingEpisodes = false;
                foreach (var xref in aniFile.EpisodeCrossRefs)
                {
                    var ep = repAniEps.GetByEpisodeID(xref.EpisodeID);
                    if (ep == null) missingEpisodes = true;
                }

                if (missingEpisodes)
                {
                    // this will then download the anime etc
                    var cmd = new CommandRequest_ProcessFile(vl.VideoLocalID, false);
                    cmd.Save();
                }
            }
        }

        public static void RunImport_ScanFolder(int importFolderID)
        {
            // get a complete list of files
            var fileList = new List<string>();
            var repFolders = new ImportFolderRepository();
            int filesFound = 0, videosFound = 0;
            var i = 0;

            try
            {
                var fldr = repFolders.GetByID(importFolderID);
                if (fldr == null) return;

                var repVidLocals = new VideoLocalRepository();
                // first build a list of files that we already know about, as we don't want to process them again
                var filesAll = repVidLocals.GetAll();
                var dictFilesExisting = new Dictionary<string, VideoLocal>();
                foreach (var vl in filesAll)
                {
                    try
                    {
                        dictFilesExisting[vl.FullServerPath] = vl;
                    }
                    catch (Exception ex)
                    {
                        var msg = string.Format("Error RunImport_ScanFolder XREF: {0} - {1}", vl.ToStringDetailed(), ex);
                        logger.Info(msg);
                    }
                }


                logger.Debug("ImportFolder: {0} || {1}", fldr.ImportFolderName, fldr.ImportFolderLocation);

                Utils.GetFilesForImportFolder(fldr.ImportFolderLocation, ref fileList);

                // get a list of all files in the share
                foreach (var fileName in fileList)
                {
                    i++;

                    if (dictFilesExisting.ContainsKey(fileName))
                    {
                        if (fldr.IsDropSource != 1)
                            continue;
                        // if this is a file in a drop source, try moving it
                        var filePath = string.Empty;
                        var nshareID = 0;
                        DataAccessHelper.GetShareAndPath(fileName, repFolders.GetAll(), ref nshareID, ref filePath);
                        var filesSearch = repVidLocals.GetByName(filePath);
                        foreach (var vid in filesSearch)
                            vid.MoveFileIfRequired();
                    }

                    filesFound++;
                    logger.Info("Processing File {0}/{1} --- {2}", i, fileList.Count, fileName);

                    if (!FileHashHelper.IsVideo(fileName)) continue;

                    videosFound++;

                    var cr_hashfile = new CommandRequest_HashFile(fileName, false);
                    cr_hashfile.Save();
                }
                logger.Debug("Found {0} new files", filesFound);
                logger.Debug("Found {0} videos", videosFound);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }


        public static void RunImport_DropFolders()
        {
            // get a complete list of files
            var fileList = new List<string>();
            var repNetShares = new ImportFolderRepository();
            foreach (var share in repNetShares.GetAll())
            {
                if (!share.FolderIsDropSource) continue;

                logger.Debug("ImportFolder: {0} || {1}", share.ImportFolderName, share.ImportFolderLocation);

                Utils.GetFilesForImportFolder(share.ImportFolderLocation, ref fileList);
            }

            // get a list of all the shares we are looking at
            int filesFound = 0, videosFound = 0;
            var i = 0;

            // get a list of all files in the share
            foreach (var fileName in fileList)
            {
                i++;
                filesFound++;
                logger.Info("Processing File {0}/{1} --- {2}", i, fileList.Count, fileName);

                if (!FileHashHelper.IsVideo(fileName)) continue;

                videosFound++;

                var cr_hashfile = new CommandRequest_HashFile(fileName, false);
                cr_hashfile.Save();
            }
            logger.Debug("Found {0} files", filesFound);
            logger.Debug("Found {0} videos", videosFound);
        }

        public static void RunImport_NewFiles()
        {
            var repVidLocals = new VideoLocalRepository();

            // first build a list of files that we already know about, as we don't want to process them again
            var filesAll = repVidLocals.GetAll();
            var dictFilesExisting = new Dictionary<string, VideoLocal>();
            foreach (var vl in filesAll)
            {
                try
                {
                    dictFilesExisting[vl.FullServerPath] = vl;
                }
                catch (Exception ex)
                {
                    var msg = string.Format("Error RunImport_NewFiles XREF: {0} - {1}", vl.ToStringDetailed(), ex);
                    logger.Info(msg);
                    //throw;
                }
            }


            // Steps for processing a file
            // 1. Check if it is a video file
            // 2. Check if we have a VideoLocal record for that file
            // .........

            // get a complete list of files
            var fileList = new List<string>();
            var repNetShares = new ImportFolderRepository();
            foreach (var share in repNetShares.GetAll())
            {
                logger.Debug("ImportFolder: {0} || {1}", share.ImportFolderName, share.ImportFolderLocation);
                try
                {
                    Utils.GetFilesForImportFolder(share.ImportFolderLocation, ref fileList);
                }
                catch (Exception ex)
                {
                    logger.ErrorException(ex.ToString(), ex);
                }
            }

            // get a list fo files that we haven't processed before
            var fileListNew = new List<string>();
            foreach (var fileName in fileList)
            {
                if (!dictFilesExisting.ContainsKey(fileName))
                    fileListNew.Add(fileName);
            }

            // get a list of all the shares we are looking at
            int filesFound = 0, videosFound = 0;
            var i = 0;

            // get a list of all files in the share
            foreach (var fileName in fileListNew)
            {
                i++;
                filesFound++;
                logger.Info("Processing File {0}/{1} --- {2}", i, fileList.Count, fileName);

                if (!FileHashHelper.IsVideo(fileName)) continue;

                videosFound++;

                var cr_hashfile = new CommandRequest_HashFile(fileName, false);
                cr_hashfile.Save();
            }
            logger.Debug("Found {0} files", filesFound);
            logger.Debug("Found {0} videos", videosFound);
        }

        public static void RunImport_GetImages()
        {
            // AniDB posters
            var repAnime = new AniDB_AnimeRepository();
            foreach (var anime in repAnime.GetAll())
            {
                if (anime.AnimeID == 8580)
                    Console.Write("");

                if (string.IsNullOrEmpty(anime.PosterPath)) continue;

                var fileExists = File.Exists(anime.PosterPath);
                if (!fileExists)
                {
                    var cmd = new CommandRequest_DownloadImage(anime.AniDB_AnimeID, JMMImageType.AniDB_Cover, false);
                    cmd.Save();
                }
            }

            // TvDB Posters
            if (ServerSettings.TvDB_AutoPosters)
            {
                var repTvPosters = new TvDB_ImagePosterRepository();
                var postersCount = new Dictionary<int, int>();

                // build a dictionary of series and how many images exist
                var allPosters = repTvPosters.GetAll();
                foreach (var tvPoster in allPosters)
                {
                    if (string.IsNullOrEmpty(tvPoster.FullImagePath)) continue;
                    var fileExists = File.Exists(tvPoster.FullImagePath);

                    if (fileExists)
                    {
                        if (postersCount.ContainsKey(tvPoster.SeriesID))
                            postersCount[tvPoster.SeriesID] = postersCount[tvPoster.SeriesID] + 1;
                        else
                            postersCount[tvPoster.SeriesID] = 1;
                    }
                }

                foreach (var tvPoster in allPosters)
                {
                    if (string.IsNullOrEmpty(tvPoster.FullImagePath)) continue;
                    var fileExists = File.Exists(tvPoster.FullImagePath);

                    var postersAvailable = 0;
                    if (postersCount.ContainsKey(tvPoster.SeriesID))
                        postersAvailable = postersCount[tvPoster.SeriesID];

                    if (!fileExists && postersAvailable < ServerSettings.TvDB_AutoPostersAmount)
                    {
                        var cmd = new CommandRequest_DownloadImage(tvPoster.TvDB_ImagePosterID, JMMImageType.TvDB_Cover,
                            false);
                        cmd.Save();

                        if (postersCount.ContainsKey(tvPoster.SeriesID))
                            postersCount[tvPoster.SeriesID] = postersCount[tvPoster.SeriesID] + 1;
                        else
                            postersCount[tvPoster.SeriesID] = 1;
                    }
                }
            }

            // TvDB Fanart
            if (ServerSettings.TvDB_AutoFanart)
            {
                var fanartCount = new Dictionary<int, int>();
                var repTvFanart = new TvDB_ImageFanartRepository();

                var allFanart = repTvFanart.GetAll();
                foreach (var tvFanart in allFanart)
                {
                    // build a dictionary of series and how many images exist
                    if (string.IsNullOrEmpty(tvFanart.FullImagePath)) continue;
                    var fileExists = File.Exists(tvFanart.FullImagePath);

                    if (fileExists)
                    {
                        if (fanartCount.ContainsKey(tvFanart.SeriesID))
                            fanartCount[tvFanart.SeriesID] = fanartCount[tvFanart.SeriesID] + 1;
                        else
                            fanartCount[tvFanart.SeriesID] = 1;
                    }
                }

                foreach (var tvFanart in allFanart)
                {
                    if (string.IsNullOrEmpty(tvFanart.FullImagePath)) continue;
                    var fileExists = File.Exists(tvFanart.FullImagePath);

                    var fanartAvailable = 0;
                    if (fanartCount.ContainsKey(tvFanart.SeriesID))
                        fanartAvailable = fanartCount[tvFanart.SeriesID];

                    if (!fileExists && fanartAvailable < ServerSettings.TvDB_AutoFanartAmount)
                    {
                        var cmd = new CommandRequest_DownloadImage(tvFanart.TvDB_ImageFanartID, JMMImageType.TvDB_FanArt,
                            false);
                        cmd.Save();

                        if (fanartCount.ContainsKey(tvFanart.SeriesID))
                            fanartCount[tvFanart.SeriesID] = fanartCount[tvFanart.SeriesID] + 1;
                        else
                            fanartCount[tvFanart.SeriesID] = 1;
                    }
                }
            }

            // TvDB Wide Banners
            if (ServerSettings.TvDB_AutoWideBanners)
            {
                var repTvBanners = new TvDB_ImageWideBannerRepository();
                var fanartCount = new Dictionary<int, int>();

                // build a dictionary of series and how many images exist
                var allBanners = repTvBanners.GetAll();
                foreach (var tvBanner in allBanners)
                {
                    if (string.IsNullOrEmpty(tvBanner.FullImagePath)) continue;
                    var fileExists = File.Exists(tvBanner.FullImagePath);

                    if (fileExists)
                    {
                        if (fanartCount.ContainsKey(tvBanner.SeriesID))
                            fanartCount[tvBanner.SeriesID] = fanartCount[tvBanner.SeriesID] + 1;
                        else
                            fanartCount[tvBanner.SeriesID] = 1;
                    }
                }

                foreach (var tvBanner in allBanners)
                {
                    if (string.IsNullOrEmpty(tvBanner.FullImagePath)) continue;
                    var fileExists = File.Exists(tvBanner.FullImagePath);

                    var bannersAvailable = 0;
                    if (fanartCount.ContainsKey(tvBanner.SeriesID))
                        bannersAvailable = fanartCount[tvBanner.SeriesID];

                    if (!fileExists && bannersAvailable < ServerSettings.TvDB_AutoWideBannersAmount)
                    {
                        var cmd = new CommandRequest_DownloadImage(tvBanner.TvDB_ImageWideBannerID,
                            JMMImageType.TvDB_Banner, false);
                        cmd.Save();

                        if (fanartCount.ContainsKey(tvBanner.SeriesID))
                            fanartCount[tvBanner.SeriesID] = fanartCount[tvBanner.SeriesID] + 1;
                        else
                            fanartCount[tvBanner.SeriesID] = 1;
                    }
                }
            }

            // TvDB Episodes
            var repTvEpisodes = new TvDB_EpisodeRepository();
            foreach (var tvEpisode in repTvEpisodes.GetAll())
            {
                if (string.IsNullOrEmpty(tvEpisode.FullImagePath)) continue;
                var fileExists = File.Exists(tvEpisode.FullImagePath);
                if (!fileExists)
                {
                    var cmd = new CommandRequest_DownloadImage(tvEpisode.TvDB_EpisodeID, JMMImageType.TvDB_Episode,
                        false);
                    cmd.Save();
                }
            }

            // MovieDB Posters
            if (ServerSettings.MovieDB_AutoPosters)
            {
                var repMoviePosters = new MovieDB_PosterRepository();
                var postersCount = new Dictionary<int, int>();

                // build a dictionary of series and how many images exist
                var allPosters = repMoviePosters.GetAll();
                foreach (var moviePoster in allPosters)
                {
                    if (string.IsNullOrEmpty(moviePoster.FullImagePath)) continue;
                    var fileExists = File.Exists(moviePoster.FullImagePath);

                    if (fileExists)
                    {
                        if (postersCount.ContainsKey(moviePoster.MovieId))
                            postersCount[moviePoster.MovieId] = postersCount[moviePoster.MovieId] + 1;
                        else
                            postersCount[moviePoster.MovieId] = 1;
                    }
                }

                foreach (var moviePoster in allPosters)
                {
                    if (string.IsNullOrEmpty(moviePoster.FullImagePath)) continue;
                    var fileExists = File.Exists(moviePoster.FullImagePath);

                    var postersAvailable = 0;
                    if (postersCount.ContainsKey(moviePoster.MovieId))
                        postersAvailable = postersCount[moviePoster.MovieId];

                    if (!fileExists && postersAvailable < ServerSettings.MovieDB_AutoPostersAmount)
                    {
                        var cmd = new CommandRequest_DownloadImage(moviePoster.MovieDB_PosterID,
                            JMMImageType.MovieDB_Poster, false);
                        cmd.Save();

                        if (postersCount.ContainsKey(moviePoster.MovieId))
                            postersCount[moviePoster.MovieId] = postersCount[moviePoster.MovieId] + 1;
                        else
                            postersCount[moviePoster.MovieId] = 1;
                    }
                }
            }

            // MovieDB Fanart
            if (ServerSettings.MovieDB_AutoFanart)
            {
                var repMovieFanarts = new MovieDB_FanartRepository();
                var fanartCount = new Dictionary<int, int>();

                // build a dictionary of series and how many images exist
                var allFanarts = repMovieFanarts.GetAll();
                foreach (var movieFanart in allFanarts)
                {
                    if (string.IsNullOrEmpty(movieFanart.FullImagePath)) continue;
                    var fileExists = File.Exists(movieFanart.FullImagePath);

                    if (fileExists)
                    {
                        if (fanartCount.ContainsKey(movieFanart.MovieId))
                            fanartCount[movieFanart.MovieId] = fanartCount[movieFanart.MovieId] + 1;
                        else
                            fanartCount[movieFanart.MovieId] = 1;
                    }
                }

                foreach (var movieFanart in repMovieFanarts.GetAll())
                {
                    if (string.IsNullOrEmpty(movieFanart.FullImagePath)) continue;
                    var fileExists = File.Exists(movieFanart.FullImagePath);

                    var fanartAvailable = 0;
                    if (fanartCount.ContainsKey(movieFanart.MovieId))
                        fanartAvailable = fanartCount[movieFanart.MovieId];

                    if (!fileExists && fanartAvailable < ServerSettings.MovieDB_AutoFanartAmount)
                    {
                        var cmd = new CommandRequest_DownloadImage(movieFanart.MovieDB_FanartID,
                            JMMImageType.MovieDB_FanArt, false);
                        cmd.Save();

                        if (fanartCount.ContainsKey(movieFanart.MovieId))
                            fanartCount[movieFanart.MovieId] = fanartCount[movieFanart.MovieId] + 1;
                        else
                            fanartCount[movieFanart.MovieId] = 1;
                    }
                }
            }

            // Trakt Posters
            if (ServerSettings.Trakt_DownloadPosters)
            {
                var repTraktPosters = new Trakt_ImagePosterRepository();
                foreach (var traktPoster in repTraktPosters.GetAll())
                {
                    if (string.IsNullOrEmpty(traktPoster.FullImagePath)) continue;
                    var fileExists = File.Exists(traktPoster.FullImagePath);
                    if (!fileExists)
                    {
                        var cmd = new CommandRequest_DownloadImage(traktPoster.Trakt_ImagePosterID,
                            JMMImageType.Trakt_Poster, false);
                        cmd.Save();
                    }
                }
            }

            // Trakt Fanart
            if (ServerSettings.Trakt_DownloadFanart)
            {
                var repTraktFanarts = new Trakt_ImageFanartRepository();
                foreach (var traktFanart in repTraktFanarts.GetAll())
                {
                    if (string.IsNullOrEmpty(traktFanart.FullImagePath)) continue;
                    var fileExists = File.Exists(traktFanart.FullImagePath);
                    if (!fileExists)
                    {
                        var cmd = new CommandRequest_DownloadImage(traktFanart.Trakt_ImageFanartID,
                            JMMImageType.Trakt_Fanart, false);
                        cmd.Save();
                    }
                }
            }

            // Trakt Episode
            if (ServerSettings.Trakt_DownloadEpisodes)
            {
                var repTraktEpisodes = new Trakt_EpisodeRepository();
                foreach (var traktEp in repTraktEpisodes.GetAll())
                {
                    if (string.IsNullOrEmpty(traktEp.FullImagePath)) continue;
                    if (!traktEp.TraktID.HasValue) continue; // if it doesn't have a TraktID it means it is old data

                    var fileExists = File.Exists(traktEp.FullImagePath);
                    if (!fileExists)
                    {
                        var cmd = new CommandRequest_DownloadImage(traktEp.Trakt_EpisodeID, JMMImageType.Trakt_Episode,
                            false);
                        cmd.Save();
                    }
                }
            }

            // AniDB Characters
            if (ServerSettings.AniDB_DownloadCharacters)
            {
                var repChars = new AniDB_CharacterRepository();
                foreach (var chr in repChars.GetAll())
                {
                    if (chr.CharID == 75250)
                    {
                        Console.WriteLine("test");
                    }

                    if (string.IsNullOrEmpty(chr.PosterPath)) continue;
                    var fileExists = File.Exists(chr.PosterPath);
                    if (!fileExists)
                    {
                        var cmd = new CommandRequest_DownloadImage(chr.AniDB_CharacterID, JMMImageType.AniDB_Character,
                            false);
                        cmd.Save();
                    }
                }
            }

            // AniDB Creators
            if (ServerSettings.AniDB_DownloadCreators)
            {
                var repSeiyuu = new AniDB_SeiyuuRepository();
                foreach (var seiyuu in repSeiyuu.GetAll())
                {
                    if (string.IsNullOrEmpty(seiyuu.PosterPath)) continue;
                    var fileExists = File.Exists(seiyuu.PosterPath);
                    if (!fileExists)
                    {
                        var cmd = new CommandRequest_DownloadImage(seiyuu.AniDB_SeiyuuID, JMMImageType.AniDB_Creator,
                            false);
                        cmd.Save();
                    }
                }
            }
        }

        public static void RunImport_ScanTvDB()
        {
            TvDBHelper.ScanForMatches();
        }

        public static void RunImport_ScanTrakt()
        {
            if (ServerSettings.Trakt_IsEnabled && !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                TraktTVHelper.ScanForMatches();
        }

        public static void RunImport_ScanMovieDB()
        {
            MovieDBHelper.ScanForMatches();
        }

        public static void RunImport_ScanMAL()
        {
            MALHelper.ScanForMatches();
        }

        public static void RunImport_UpdateTvDB(bool forced)
        {
            TvDBHelper.UpdateAllInfo(forced);
        }

        public static void RunImport_UpdateAllAniDB()
        {
            var repAnime = new AniDB_AnimeRepository();
            foreach (var anime in repAnime.GetAll())
            {
                var cmd = new CommandRequest_GetAnimeHTTP(anime.AnimeID, true, false);
                cmd.Save();
            }
        }

        public static void RemoveRecordsWithoutPhysicalFiles()
        {
            var repVidLocals = new VideoLocalRepository();
            var repXRefs = new CrossRef_File_EpisodeRepository();

            // get a full list of files
            var filesAll = repVidLocals.GetAll();
            foreach (var vl in filesAll)
            {
                if (!File.Exists(vl.FullServerPath))
                {
                    // delete video local record
                    logger.Info("RemoveRecordsWithoutPhysicalFiles : {0}", vl.FullServerPath);
                    repVidLocals.Delete(vl.VideoLocalID);

                    var cmdDel = new CommandRequest_DeleteFileFromMyList(vl.Hash, vl.FileSize);
                    cmdDel.Save();
                }
            }

            UpdateAllStats();
        }


        public static string DeleteImportFolder(int importFolderID)
        {
            try
            {
                var repNS = new ImportFolderRepository();
                var ns = repNS.GetByID(importFolderID);

                if (ns == null) return "Could not find Import Folder ID: " + importFolderID;

                // first delete all the files attached  to this import folder
                var affectedSeries = new Dictionary<int, AnimeSeries>();

                var repVids = new VideoLocalRepository();
                foreach (var vid in repVids.GetByImportFolder(importFolderID))
                {
                    //Thread.Sleep(5000);
                    logger.Info("Deleting video local record: {0}", vid.FullServerPath);

                    AnimeSeries ser = null;
                    var animeEpisodes = vid.GetAnimeEpisodes();
                    if (animeEpisodes.Count > 0)
                    {
                        ser = animeEpisodes[0].GetAnimeSeries();
                        if (ser != null && !affectedSeries.ContainsKey(ser.AnimeSeriesID))
                            affectedSeries.Add(ser.AnimeSeriesID, ser);
                    }

                    repVids.Delete(vid.VideoLocalID);
                }

                // delete any duplicate file records which reference this folder
                var repDupFiles = new DuplicateFileRepository();
                foreach (var df in repDupFiles.GetByImportFolder1(importFolderID))
                    repDupFiles.Delete(df.DuplicateFileID);

                foreach (var df in repDupFiles.GetByImportFolder2(importFolderID))
                    repDupFiles.Delete(df.DuplicateFileID);

                // delete the import folder
                repNS.Delete(importFolderID);
                ServerInfo.Instance.RefreshImportFolders();

                foreach (var ser in affectedSeries.Values)
                {
                    ser.QueueUpdateStats();
                    //StatsCache.Instance.UpdateUsingSeries(ser.AnimeSeriesID);
                }


                return "";
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return ex.Message;
            }
        }

        public static void UpdateAllStats()
        {
            var repSeries = new AnimeSeriesRepository();
            foreach (var ser in repSeries.GetAll())
            {
                ser.QueueUpdateStats();
            }
        }


        public static int UpdateAniDBFileData(bool missingInfo, bool outOfDate, bool countOnly)
        {
            var vidsToUpdate = new List<int>();
            try
            {
                var repFiles = new AniDB_FileRepository();
                var repVids = new VideoLocalRepository();

                if (missingInfo)
                {
                    var vids = repVids.GetByAniDBResolution("0x0");

                    foreach (var vid in vids)
                    {
                        if (!vidsToUpdate.Contains(vid.VideoLocalID))
                            vidsToUpdate.Add(vid.VideoLocalID);
                    }
                }

                if (outOfDate)
                {
                    var vids = repVids.GetByInternalVersion(1);

                    foreach (var vid in vids)
                    {
                        if (!vidsToUpdate.Contains(vid.VideoLocalID))
                            vidsToUpdate.Add(vid.VideoLocalID);
                    }
                }

                if (!countOnly)
                {
                    foreach (var id in vidsToUpdate)
                    {
                        var cmd = new CommandRequest_GetFile(id, true);
                        cmd.Save();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }

            return vidsToUpdate.Count;
        }

        public static void CheckForTvDBUpdates(bool forceRefresh)
        {
            if (ServerSettings.TvDB_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            var freqHours = Utils.GetScheduledHours(ServerSettings.TvDB_UpdateFrequency);

            // update tvdb info every 12 hours
            var repSched = new ScheduledUpdateRepository();

            var sched = repSched.GetByUpdateType((int)ScheduledUpdateType.TvDBInfo);
            if (sched != null)
            {
                // if we have run this in the last 12 hours and are not forcing it, then exit
                var tsLastRun = DateTime.Now - sched.LastUpdate;
                if (tsLastRun.TotalHours < freqHours)
                {
                    if (!forceRefresh) return;
                }
            }

            var tvDBIDs = new List<int>();
            var tvDBOnline = false;
            var serverTime = JMMService.TvdbHelper.IncrementalTvDBUpdate(ref tvDBIDs, ref tvDBOnline);

            if (tvDBOnline)
            {
                foreach (var tvid in tvDBIDs)
                {
                    // download and update series info, episode info and episode images
                    // will also download fanart, posters and wide banners
                    var cmdSeriesEps = new CommandRequest_TvDBUpdateSeriesAndEpisodes(tvid, false);
                    cmdSeriesEps.Save();
                }
            }

            if (sched == null)
            {
                sched = new ScheduledUpdate();
                sched.UpdateType = (int)ScheduledUpdateType.TvDBInfo;
            }

            sched.LastUpdate = DateTime.Now;
            sched.UpdateDetails = serverTime;
            repSched.Save(sched);

            TvDBHelper.ScanForMatches();
        }

        public static void CheckForCalendarUpdate(bool forceRefresh)
        {
            if (ServerSettings.AniDB_Calendar_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh)
                return;
            var freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_Calendar_UpdateFrequency);

            // update the calendar every 12 hours
            // we will always assume that an anime was downloaded via http first
            var repSched = new ScheduledUpdateRepository();
            var repAnime = new AniDB_AnimeRepository();

            var sched = repSched.GetByUpdateType((int)ScheduledUpdateType.AniDBCalendar);
            if (sched != null)
            {
                // if we have run this in the last 12 hours and are not forcing it, then exit
                var tsLastRun = DateTime.Now - sched.LastUpdate;
                if (tsLastRun.TotalHours < freqHours)
                {
                    if (!forceRefresh) return;
                }
            }

            var cmd = new CommandRequest_GetCalendar(forceRefresh);
            cmd.Save();
        }

        public static void SendUserInfoUpdate(bool forceRefresh)
        {
            // update the anonymous user info every 12 hours
            // we will always assume that an anime was downloaded via http first
            var repSched = new ScheduledUpdateRepository();

            var sched = repSched.GetByUpdateType((int)ScheduledUpdateType.AzureUserInfo);
            if (sched != null)
            {
                // if we have run this in the last 6 hours and are not forcing it, then exit
                var tsLastRun = DateTime.Now - sched.LastUpdate;
                if (tsLastRun.TotalHours < 6)
                {
                    if (!forceRefresh) return;
                }
            }

            if (sched == null)
            {
                sched = new ScheduledUpdate();
                sched.UpdateType = (int)ScheduledUpdateType.AzureUserInfo;
                sched.UpdateDetails = "";
            }
            sched.LastUpdate = DateTime.Now;
            repSched.Save(sched);

            var cmd = new CommandRequest_Azure_SendUserInfo(ServerSettings.AniDB_Username);
            cmd.Save();
        }

        public static void CheckForAnimeUpdate(bool forceRefresh)
        {
            if (ServerSettings.AniDB_Anime_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            var freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_Anime_UpdateFrequency);

            // check for any updated anime info every 12 hours
            var repSched = new ScheduledUpdateRepository();
            var repAnime = new AniDB_AnimeRepository();

            var sched = repSched.GetByUpdateType((int)ScheduledUpdateType.AniDBUpdates);
            if (sched != null)
            {
                // if we have run this in the last 12 hours and are not forcing it, then exit
                var tsLastRun = DateTime.Now - sched.LastUpdate;
                if (tsLastRun.TotalHours < freqHours)
                {
                    if (!forceRefresh) return;
                }
            }

            var cmd = new CommandRequest_GetUpdated(true);
            cmd.Save();
        }

        public static void CheckForMALUpdate(bool forceRefresh)
        {
            if (ServerSettings.AniDB_Anime_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            var freqHours = Utils.GetScheduledHours(ServerSettings.MAL_UpdateFrequency);

            // check for any updated anime info every 12 hours
            var repSched = new ScheduledUpdateRepository();
            var repAnime = new AniDB_AnimeRepository();

            var sched = repSched.GetByUpdateType((int)ScheduledUpdateType.MALUpdate);
            if (sched != null)
            {
                // if we have run this in the last 12 hours and are not forcing it, then exit
                var tsLastRun = DateTime.Now - sched.LastUpdate;
                if (tsLastRun.TotalHours < freqHours)
                {
                    if (!forceRefresh) return;
                }
            }

            RunImport_ScanMAL();

            if (sched == null)
            {
                sched = new ScheduledUpdate();
                sched.UpdateType = (int)ScheduledUpdateType.MALUpdate;
                sched.UpdateDetails = "";
            }
            sched.LastUpdate = DateTime.Now;
            repSched.Save(sched);
        }

        public static void CheckForMyListStatsUpdate(bool forceRefresh)
        {
            if (ServerSettings.AniDB_MyListStats_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh)
                return;
            var freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_MyListStats_UpdateFrequency);

            var repSched = new ScheduledUpdateRepository();

            var sched = repSched.GetByUpdateType((int)ScheduledUpdateType.AniDBMylistStats);
            if (sched != null)
            {
                // if we have run this in the last 24 hours and are not forcing it, then exit
                var tsLastRun = DateTime.Now - sched.LastUpdate;
                logger.Trace("Last AniDB MyList Stats Update: {0} minutes ago", tsLastRun.TotalMinutes);
                if (tsLastRun.TotalHours < freqHours)
                {
                    if (!forceRefresh) return;
                }
            }

            var cmd = new CommandRequest_UpdateMylistStats(forceRefresh);
            cmd.Save();
        }

        public static void CheckForMyListSyncUpdate(bool forceRefresh)
        {
            if (ServerSettings.AniDB_MyList_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            var freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_MyList_UpdateFrequency);

            // update the calendar every 24 hours
            var repSched = new ScheduledUpdateRepository();

            var sched = repSched.GetByUpdateType((int)ScheduledUpdateType.AniDBMyListSync);
            if (sched != null)
            {
                // if we have run this in the last 24 hours and are not forcing it, then exit
                var tsLastRun = DateTime.Now - sched.LastUpdate;
                logger.Trace("Last AniDB MyList Sync: {0} minutes ago", tsLastRun.TotalMinutes);
                if (tsLastRun.TotalHours < freqHours)
                {
                    if (!forceRefresh) return;
                }
            }

            var cmd = new CommandRequest_SyncMyList(forceRefresh);
            cmd.Save();
        }

        public static void CheckForTraktSyncUpdate(bool forceRefresh)
        {
            if (ServerSettings.Trakt_SyncFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            var freqHours = Utils.GetScheduledHours(ServerSettings.Trakt_SyncFrequency);

            // update the calendar every xxx hours
            var repSched = new ScheduledUpdateRepository();

            var sched = repSched.GetByUpdateType((int)ScheduledUpdateType.TraktSync);
            if (sched != null)
            {
                // if we have run this in the last xxx hours and are not forcing it, then exit
                var tsLastRun = DateTime.Now - sched.LastUpdate;
                logger.Trace("Last Trakt Sync: {0} minutes ago", tsLastRun.TotalMinutes);
                if (tsLastRun.TotalHours < freqHours)
                {
                    if (!forceRefresh) return;
                }
            }

            if (ServerSettings.Trakt_IsEnabled && !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
            {
                var cmd = new CommandRequest_TraktSyncCollection(false);
                cmd.Save();
            }
        }

        public static void CheckForTraktAllSeriesUpdate(bool forceRefresh)
        {
            if (ServerSettings.Trakt_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            var freqHours = Utils.GetScheduledHours(ServerSettings.Trakt_UpdateFrequency);

            // update the calendar every xxx hours
            var repSched = new ScheduledUpdateRepository();

            var sched = repSched.GetByUpdateType((int)ScheduledUpdateType.TraktUpdate);
            if (sched != null)
            {
                // if we have run this in the last xxx hours and are not forcing it, then exit
                var tsLastRun = DateTime.Now - sched.LastUpdate;
                logger.Trace("Last Trakt Update: {0} minutes ago", tsLastRun.TotalMinutes);
                if (tsLastRun.TotalHours < freqHours)
                {
                    if (!forceRefresh) return;
                }
            }

            var cmd = new CommandRequest_TraktUpdateAllSeries(false);
            cmd.Save();
        }

        public static void CheckForTraktTokenUpdate(bool forceRefresh)
        {
            try
            {
                // by updating the Trakt token regularly, the user won't need to authorize again
                var freqHours = 24; // we need to update this daily

                var repSched = new ScheduledUpdateRepository();

                var sched = repSched.GetByUpdateType((int)ScheduledUpdateType.TraktToken);
                if (sched != null)
                {
                    // if we have run this in the last xxx hours and are not forcing it, then exit
                    var tsLastRun = DateTime.Now - sched.LastUpdate;
                    logger.Trace("Last Trakt Token Update: {0} minutes ago", tsLastRun.TotalMinutes);
                    if (tsLastRun.TotalHours < freqHours)
                    {
                        if (!forceRefresh) return;
                    }
                }

                TraktTVHelper.RefreshAuthToken();
                if (sched == null)
                {
                    sched = new ScheduledUpdate();
                    sched.UpdateType = (int)ScheduledUpdateType.TraktToken;
                    sched.UpdateDetails = "";
                }
                sched.LastUpdate = DateTime.Now;
                repSched.Save(sched);
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error in CheckForTraktTokenUpdate: " + ex, ex);
            }
        }

        public static void CheckForAniDBFileUpdate(bool forceRefresh)
        {
            if (ServerSettings.AniDB_File_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            var freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_File_UpdateFrequency);

            // check for any updated anime info every 12 hours
            var repSched = new ScheduledUpdateRepository();
            var repAnime = new AniDB_AnimeRepository();

            var sched = repSched.GetByUpdateType((int)ScheduledUpdateType.AniDBFileUpdates);
            if (sched != null)
            {
                // if we have run this in the last 12 hours and are not forcing it, then exit
                var tsLastRun = DateTime.Now - sched.LastUpdate;
                if (tsLastRun.TotalHours < freqHours)
                {
                    if (!forceRefresh) return;
                }
            }

            UpdateAniDBFileData(true, false, false);

            // files which have been hashed, but don't have an associated episode
            var repVidLocals = new VideoLocalRepository();
            var filesWithoutEpisode = repVidLocals.GetVideosWithoutEpisode();

            foreach (var vl in filesWithoutEpisode)
            {
                var cmd = new CommandRequest_ProcessFile(vl.VideoLocalID, true);
                cmd.Save();
            }

            // now check for any files which have been manually linked and are less than 30 days old


            if (sched == null)
            {
                sched = new ScheduledUpdate();
                sched.UpdateType = (int)ScheduledUpdateType.AniDBFileUpdates;
                sched.UpdateDetails = "";
            }
            sched.LastUpdate = DateTime.Now;
            repSched.Save(sched);
        }

        public static void UpdateAniDBTitles()
        {
            var freqHours = 100;

            var process =
                ServerSettings.AniDB_Username.Equals("jonbaby", StringComparison.InvariantCultureIgnoreCase) ||
                ServerSettings.AniDB_Username.Equals("jmediamanager", StringComparison.InvariantCultureIgnoreCase);

            if (!process) return;

            // check for any updated anime info every 100 hours
            var repSched = new ScheduledUpdateRepository();
            var repAnime = new AniDB_AnimeRepository();

            var sched = repSched.GetByUpdateType((int)ScheduledUpdateType.AniDBTitles);
            if (sched != null)
            {
                // if we have run this in the last 100 hours and are not forcing it, then exit
                var tsLastRun = DateTime.Now - sched.LastUpdate;
                if (tsLastRun.TotalHours < freqHours) return;
            }

            if (sched == null)
            {
                sched = new ScheduledUpdate();
                sched.UpdateType = (int)ScheduledUpdateType.AniDBTitles;
                sched.UpdateDetails = "";
            }
            sched.LastUpdate = DateTime.Now;
            repSched.Save(sched);

            var cmd = new CommandRequest_GetAniDBTitles();
            cmd.Save();
        }
    }
}