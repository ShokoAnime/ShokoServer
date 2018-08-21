using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Shoko.Models.Server;
using Shoko.Models.Azure;
using Shoko.Models.Enums;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
//using Shoko.Server.Commands.Azure;
using NLog;
using Shoko.Server.Databases;
using NutzCode.CloudFileSystem;
using Shoko.Commons.Extensions;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.FileHelper;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Commands.Azure;

namespace Shoko.Server
{
    public class Importer
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static void RunImport_IntegrityCheck()
        {
            // files which have not been hashed yet
            // or files which do not have a VideoInfo record
            List<SVR_VideoLocal> filesToHash = Repo.VideoLocal.GetVideosWithoutHash();
            Dictionary<int, SVR_VideoLocal> dictFilesToHash = new Dictionary<int, SVR_VideoLocal>();
            foreach (SVR_VideoLocal vl in filesToHash)
            {
                dictFilesToHash[vl.VideoLocalID] = vl;
                SVR_VideoLocal_Place p = vl.GetBestVideoLocalPlace(true);
                if (p != null)
                {
                    CommandRequest_HashFile cmd = new CommandRequest_HashFile(p.FullServerPath, false);
                    cmd.Save();
                }
            }

            foreach (SVR_VideoLocal vl in filesToHash)
            {
                // don't use if it is in the previous list
                if (dictFilesToHash.ContainsKey(vl.VideoLocalID)) continue;
                try
                {
                    SVR_VideoLocal_Place p = vl.GetBestVideoLocalPlace(true);
                    if (p != null)
                    {
                        CommandRequest_HashFile cmd = new CommandRequest_HashFile(p.FullServerPath, false);
                        cmd.Save();
                    }
                }
                catch (Exception ex)
                {
                    string msg = $"Error RunImport_IntegrityCheck XREF: {vl.ToStringDetailed()} - {ex}";
                    logger.Info(msg);
                }
            }

            // files which have been hashed, but don't have an associated episode
            foreach (SVR_VideoLocal v in Repo.VideoLocal.GetVideosWithoutEpisode()
                .Where(a => !string.IsNullOrEmpty(a.Hash)))
            {
                CommandRequest_ProcessFile cmd = new CommandRequest_ProcessFile(v.VideoLocalID, false);
                cmd.Save();
            }

            // check that all the episode data is populated
            foreach (SVR_VideoLocal vl in Repo.VideoLocal.GetAll().Where(a => !string.IsNullOrEmpty(a.Hash)))
            {
                // if the file is not manually associated, then check for AniDB_File info
                SVR_AniDB_File aniFile = Repo.AniDB_File.GetByHash(vl.Hash);
                foreach (CrossRef_File_Episode xref in vl.EpisodeCrossRefs)
                {
                    if (xref.CrossRefSource != (int) CrossRefSource.AniDB) continue;
                    if (aniFile == null)
                    {
                        CommandRequest_ProcessFile cmd = new CommandRequest_ProcessFile(vl.VideoLocalID, false);
                        cmd.Save();
                    }
                }

                if (aniFile == null) continue;

                // the cross ref is created before the actually episode data is downloaded
                // so lets check for that
                bool missingEpisodes = false;
                foreach (CrossRef_File_Episode xref in aniFile.EpisodeCrossRefs)
                {
                    AniDB_Episode ep = Repo.AniDB_Episode.GetByEpisodeID(xref.EpisodeID);
                    if (ep == null) missingEpisodes = true;
                }

                if (missingEpisodes)
                {
                    // this will then download the anime etc
                    CommandRequest_ProcessFile cmd = new CommandRequest_ProcessFile(vl.VideoLocalID, false);
                    cmd.Save();
                }
            }
        }

        public static void SyncMedia()
        {
            List<SVR_VideoLocal> allfiles = Repo.VideoLocal.GetAll().ToList();
            AzureWebAPI.Send_Media(allfiles);
        }

        public static void SyncHashes()
        {
            bool paused = ShokoService.CmdProcessorHasher.Paused;
            ShokoService.CmdProcessorHasher.Paused = true;
            List<SVR_VideoLocal> allfiles = Repo.VideoLocal.GetAll().ToList();
            List<SVR_VideoLocal> missfiles = allfiles.Where(
                    a =>
                        string.IsNullOrEmpty(a.CRC32) || string.IsNullOrEmpty(a.SHA1) ||
                        string.IsNullOrEmpty(a.MD5) || a.SHA1 == "0000000000000000000000000000000000000000" ||
                        a.MD5 == "00000000000000000000000000000000")
                .ToList();
            List<SVR_VideoLocal> withfiles = allfiles.Except(missfiles).ToList();
            Dictionary<int,(string ed2k, string crc32, string md5, string sha1)> updates=new Dictionary<int, (string ed2k, string crc32, string md5, string sha1)>();

            //Check if we can populate md5,sha and crc from AniDB_Files
            foreach (SVR_VideoLocal v in missfiles.ToList())
            {
                ShokoService.CmdProcessorHasher.QueueState = new QueueStateStruct()
                {
                    queueState = QueueStateEnum.CheckingFile,
                    extraParams = new[] {v.FileName}
                };
                SVR_AniDB_File file = Repo.AniDB_File.GetByHash(v.ED2KHash);
                if (file != null)
                {
                    if (!string.IsNullOrEmpty(file.CRC) && !string.IsNullOrEmpty(file.SHA1) &&
                        !string.IsNullOrEmpty(file.MD5))
                    {
                        updates[v.VideoLocalID]=(file.Hash, file.CRC,file.MD5,file.SHA1);
                        missfiles.Remove(v);
                        withfiles.Add(v);
                        continue;
                    }
                }
                List<Azure_FileHash> ls = AzureWebAPI.Get_FileHash(FileHashType.ED2K, v.ED2KHash);
                if (ls != null)
                {
                    ls = ls.Where(
                            a =>
                                !string.IsNullOrEmpty(a.CRC32) && !string.IsNullOrEmpty(a.MD5) &&
                                !string.IsNullOrEmpty(a.SHA1))
                        .ToList();
                    if (ls.Count > 0)
                    {
                        updates[v.VideoLocalID] = (ls[0].ED2K.ToUpperInvariant(),ls[0].CRC32.ToUpperInvariant(), ls[0].MD5.ToUpperInvariant(), ls[0].SHA1.ToUpperInvariant());
                        missfiles.Remove(v);
                    }
                }
            }

            //We need to recalculate the sha1, md5 and crc32 of the missing ones.
            List<SVR_VideoLocal> tosend = new List<SVR_VideoLocal>();
            foreach (SVR_VideoLocal v in missfiles)
            {
                try
                {
                    SVR_VideoLocal_Place p = v.GetBestVideoLocalPlace();
                    if (p != null && p.ImportFolder.CloudID == 0)
                    {
                        ShokoService.CmdProcessorHasher.QueueState = new QueueStateStruct()
                        {
                            queueState = QueueStateEnum.HashingFile,
                            extraParams = new[] {v.FileName}
                        };
                        Hashes h = FileHashHelper.GetHashInfo(p.FullServerPath, true, ShokoServer.OnHashProgress,
                            true,
                            true,
                            true);
                        updates[v.VideoLocalID] = (h.ED2K, h.CRC32, h.MD5, h.SHA1);
                        v.Hash = h.ED2K;
                        v.CRC32 = h.CRC32;
                        v.MD5 = h.MD5;
                        v.SHA1 = h.SHA1;
                        v.HashSource = (int) HashSource.DirectHash;
                        withfiles.Add(v);
                    }
                }
                catch
                {
                    //Ignored
                }
            }
            if (updates.Count > 0)
            {
                using (var upd = Repo.VideoLocal.BeginBatchUpdate(() => Repo.VideoLocal.GetMany(updates.Keys)))
                {
                    foreach (SVR_VideoLocal v in upd)
                    {
                        (string ed2k, string crc32, string md5, string sha1) t = updates[v.VideoLocalID];
                        v.Hash = t.ed2k;
                        v.CRC32 = t.crc32;
                        v.MD5 = t.md5;
                        v.SHA1 = t.sha1;
                        upd.Update(v);
                    }
                    upd.Commit();
                }
            }
            //Send the hashes
            AzureWebAPI.Send_FileHash(withfiles);
            logger.Info("Sync Hashes Complete");

            ShokoService.CmdProcessorHasher.Paused = paused;
        }

        public static void RunImport_ScanFolder(int importFolderID)
        {
            // get a complete list of files
            List<string> fileList = new List<string>();
            int filesFound = 0, videosFound = 0;
            int i = 0;

            try
            {
                SVR_ImportFolder fldr = Repo.ImportFolder.GetByID(importFolderID);
                if (fldr == null) return;

                // first build a list of files that we already know about, as we don't want to process them again

                List<SVR_VideoLocal_Place> filesAll =
                    Repo.VideoLocal_Place.GetByImportFolder(fldr.ImportFolderID);
                Dictionary<string, SVR_VideoLocal_Place> dictFilesExisting =
                    new Dictionary<string, SVR_VideoLocal_Place>();
                foreach (SVR_VideoLocal_Place vl in filesAll)
                {
                    try
                    {
                        dictFilesExisting[vl.FullServerPath] = vl;
                    }
                    catch (Exception ex)
                    {
                        string msg = string.Format("Error RunImport_ScanFolder XREF: {0} - {1}", vl.FullServerPath,
                            ex.ToString());
                        logger.Info(msg);
                    }
                }

                logger.Debug("ImportFolder: {0} || {1}", fldr.ImportFolderName, fldr.ImportFolderLocation);
                Utils.GetFilesForImportFolder(fldr.BaseDirectory, ref fileList);

                // Get Ignored Files and remove them from the scan listing
                var ignoredFiles = Repo.VideoLocal.GetIgnoredVideos().SelectMany(a => a.Places)
                    .Select(a => a.FullServerPath).Where(a => !string.IsNullOrEmpty(a) ).ToList();
                fileList = fileList.Except(ignoredFiles, StringComparer.InvariantCultureIgnoreCase).ToList();

                // get a list of all files in the share
                foreach (string fileName in fileList)
                {
                    i++;

                    if (dictFilesExisting.ContainsKey(fileName))
                    {
                        if (fldr.IsDropSource == 1)
                            dictFilesExisting[fileName].RenameAndMoveAsRequired();
                    }
                    if (fileName.Contains("$RECYCLE.BIN")) continue;

                    filesFound++;
                    logger.Trace("Processing File {0}/{1} --- {2}", i, fileList.Count, fileName);

                    if (!FileHashHelper.IsVideo(fileName)) continue;

                    videosFound++;

                    CommandRequest_HashFile cr_hashfile = new CommandRequest_HashFile(fileName, false);
                    cr_hashfile.Save();
                }
                logger.Debug("Found {0} new files", filesFound);
                logger.Debug("Found {0} videos", videosFound);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        public static void RunImport_DropFolders()
        {
            // get a complete list of files
            List<string> fileList = new List<string>();
            foreach (SVR_ImportFolder share in Repo.ImportFolder.GetAll())
            {
                if (!share.FolderIsDropSource) continue;

                logger.Debug("ImportFolder: {0} || {1}", share.ImportFolderName, share.ImportFolderLocation);
                Utils.GetFilesForImportFolder(share.BaseDirectory, ref fileList);
            }

            // Get Ignored Files and remove them from the scan listing
            var ignoredFiles = Repo.VideoLocal.GetIgnoredVideos().SelectMany(a => a.Places)
                .Select(a => a.FullServerPath).Where(a => !string.IsNullOrEmpty(a)).ToList();
            fileList = fileList.Except(ignoredFiles, StringComparer.InvariantCultureIgnoreCase).ToList();

            // get a list of all the shares we are looking at
            int filesFound = 0, videosFound = 0;
            int i = 0;

            // get a list of all files in the share
            foreach (string fileName in fileList)
            {
                i++;
                if (fileName.Contains("$RECYCLE.BIN")) continue;
                filesFound++;
                logger.Trace("Processing File {0}/{1} --- {2}", i, fileList.Count, fileName);

                if (!FileHashHelper.IsVideo(fileName)) continue;

                videosFound++;

                CommandRequest_HashFile cr_hashfile = new CommandRequest_HashFile(fileName, false);
                cr_hashfile.Save();
            }
            logger.Debug("Found {0} files", filesFound);
            logger.Debug("Found {0} videos", videosFound);
        }

        public static void RunImport_NewFiles()
        {
            // first build a list of files that we already know about, as we don't want to process them again
            IReadOnlyList<SVR_VideoLocal_Place> filesAll = Repo.VideoLocal_Place.GetAll();
            Dictionary<string, SVR_VideoLocal_Place> dictFilesExisting = new Dictionary<string, SVR_VideoLocal_Place>();
            foreach (SVR_VideoLocal_Place vl in filesAll)
            {
                try
                {
                    if (vl.FullServerPath == null)
                    {
                        logger.Info("Invalid File Path found. Removing: " + vl.VideoLocal_Place_ID);
                        vl.RemoveRecord();
                        continue;
                    }
                    dictFilesExisting[vl.FullServerPath] = vl;
                }
                catch (Exception ex)
                {
                    string msg = string.Format("Error RunImport_NewFiles XREF: {0} - {1}",
                        ((vl.FullServerPath ?? vl.FilePath) ?? vl.VideoLocal_Place_ID.ToString()),
                        ex.ToString());
                    logger.Error(msg);
                    //throw;
                }
            }

            // Steps for processing a file
            // 1. Check if it is a video file
            // 2. Check if we have a VideoLocal record for that file
            // .........

            // get a complete list of files
            List<string> fileList = new List<string>();
            foreach (SVR_ImportFolder share in Repo.ImportFolder.GetAll())
            {
                logger.Debug("ImportFolder: {0} || {1}", share.ImportFolderName, share.ImportFolderLocation);
                try
                {
                    Utils.GetFilesForImportFolder(share.BaseDirectory, ref fileList);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, ex.ToString());
                }
            }

            // get a list fo files that we haven't processed before
            List<string> fileListNew = new List<string>();
            foreach (string fileName in fileList)
            {
                if (fileName.Contains("$RECYCLE.BIN")) continue;
                if (!dictFilesExisting.ContainsKey(fileName))
                    fileListNew.Add(fileName);
            }

            // get a list of all the shares we are looking at
            int filesFound = 0, videosFound = 0;
            int i = 0;

            // get a list of all files in the share
            foreach (string fileName in fileListNew)
            {
                i++;
                filesFound++;
                logger.Trace("Processing File {0}/{1} --- {2}", i, fileList.Count, fileName);

                if (!FileHashHelper.IsVideo(fileName)) continue;

                videosFound++;

                CommandRequest_HashFile cr_hashfile = new CommandRequest_HashFile(fileName, false);
                cr_hashfile.Save();
            }
            logger.Debug("Found {0} files", filesFound);
            logger.Debug("Found {0} videos", videosFound);
        }

        public static void RunImport_ImportFolderNewFiles(SVR_ImportFolder fldr)
        {
            List<string> fileList = new List<string>();
            int filesFound = 0, videosFound = 0;
            int i = 0;
            List<SVR_VideoLocal_Place> filesAll = Repo.VideoLocal_Place.GetByImportFolder(fldr.ImportFolderID);
            Utils.GetFilesForImportFolder(fldr.BaseDirectory, ref fileList);

            HashSet<string> fs = new HashSet<string>(fileList);
            foreach (SVR_VideoLocal_Place v in filesAll)
            {
                if (fs.Contains(v.FullServerPath))
                    fileList.Remove(v.FullServerPath);
            }

            // get a list of all files in the share
            foreach (string fileName in fileList)
            {
                i++;
                if (fileName.Contains("$RECYCLE.BIN")) continue;
                filesFound++;
                logger.Trace("Processing File {0}/{1} --- {2}", i, fileList.Count, fileName);

                if (!FileHashHelper.IsVideo(fileName)) continue;

                videosFound++;

                CommandRequest_HashFile cr_hashfile = new CommandRequest_HashFile(fileName, false);
                cr_hashfile.Save();
            }
            logger.Debug("Found {0} files", filesFound);
            logger.Debug("Found {0} videos", videosFound);
        }

        public static void RunImport_GetImages()
        {
            // AniDB posters
            foreach (SVR_AniDB_Anime anime in Repo.AniDB_Anime.GetAll())
            {
                if (anime.AnimeID == 8580)
                    Console.Write("");

                if (string.IsNullOrEmpty(anime.PosterPath)) continue;

                bool fileExists = File.Exists(anime.PosterPath);
                if (!fileExists)
                {
                    CommandRequest_DownloadAniDBImages cmd = new CommandRequest_DownloadAniDBImages(anime.AnimeID, false);
                    cmd.Save();
                }
            }

            // TvDB Posters
            if (ServerSettings.Instance.TvDB_AutoPosters)
            {
                Dictionary<int, int> postersCount = new Dictionary<int, int>();

                // build a dictionary of series and how many images exist
                IReadOnlyList<TvDB_ImagePoster> allPosters = Repo.TvDB_ImagePoster.GetAll();
                foreach (TvDB_ImagePoster tvPoster in allPosters)
                {
                    if (string.IsNullOrEmpty(tvPoster.GetFullImagePath())) continue;
                    bool fileExists = File.Exists(tvPoster.GetFullImagePath());

                    if (fileExists)
                    {
                        if (postersCount.ContainsKey(tvPoster.SeriesID))
                            postersCount[tvPoster.SeriesID] = postersCount[tvPoster.SeriesID] + 1;
                        else
                            postersCount[tvPoster.SeriesID] = 1;
                    }
                }

                foreach (TvDB_ImagePoster tvPoster in allPosters)
                {
                    if (string.IsNullOrEmpty(tvPoster.GetFullImagePath())) continue;
                    bool fileExists = File.Exists(tvPoster.GetFullImagePath());

                    int postersAvailable = 0;
                    if (postersCount.ContainsKey(tvPoster.SeriesID))
                        postersAvailable = postersCount[tvPoster.SeriesID];

                    if (!fileExists && postersAvailable < ServerSettings.Instance.TvDB_AutoPostersAmount)
                    {
                        CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(tvPoster.TvDB_ImagePosterID,
                            ImageEntityType.TvDB_Cover, false);
                        cmd.Save();

                        if (postersCount.ContainsKey(tvPoster.SeriesID))
                            postersCount[tvPoster.SeriesID] = postersCount[tvPoster.SeriesID] + 1;
                        else
                            postersCount[tvPoster.SeriesID] = 1;
                    }
                }
            }

            // TvDB Fanart
            if (ServerSettings.Instance.TvDB_AutoFanart)
            {
                Dictionary<int, int> fanartCount = new Dictionary<int, int>();
                IReadOnlyList<TvDB_ImageFanart> allFanart = Repo.TvDB_ImageFanart.GetAll();
                foreach (TvDB_ImageFanart tvFanart in allFanart)
                {
                    // build a dictionary of series and how many images exist
                    if (string.IsNullOrEmpty(tvFanart.GetFullImagePath())) continue;
                    bool fileExists = File.Exists(tvFanart.GetFullImagePath());

                    if (fileExists)
                    {
                        if (fanartCount.ContainsKey(tvFanart.SeriesID))
                            fanartCount[tvFanart.SeriesID] = fanartCount[tvFanart.SeriesID] + 1;
                        else
                            fanartCount[tvFanart.SeriesID] = 1;
                    }
                }

                foreach (TvDB_ImageFanart tvFanart in allFanart)
                {
                    if (string.IsNullOrEmpty(tvFanart.GetFullImagePath())) continue;
                    bool fileExists = File.Exists(tvFanart.GetFullImagePath());

                    int fanartAvailable = 0;
                    if (fanartCount.ContainsKey(tvFanart.SeriesID))
                        fanartAvailable = fanartCount[tvFanart.SeriesID];

                    if (!fileExists && fanartAvailable < ServerSettings.Instance.TvDB_AutoFanartAmount)
                    {
                        CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(tvFanart.TvDB_ImageFanartID,
                            ImageEntityType.TvDB_FanArt, false);
                        cmd.Save();

                        if (fanartCount.ContainsKey(tvFanart.SeriesID))
                            fanartCount[tvFanart.SeriesID] = fanartCount[tvFanart.SeriesID] + 1;
                        else
                            fanartCount[tvFanart.SeriesID] = 1;
                    }
                }
            }

            // TvDB Wide Banners
            if (ServerSettings.Instance.TvDB_AutoWideBanners)
            {
                Dictionary<int, int> fanartCount = new Dictionary<int, int>();

                // build a dictionary of series and how many images exist
                IReadOnlyList<TvDB_ImageWideBanner> allBanners = Repo.TvDB_ImageWideBanner.GetAll();
                foreach (TvDB_ImageWideBanner tvBanner in allBanners)
                {
                    if (string.IsNullOrEmpty(tvBanner.GetFullImagePath())) continue;
                    bool fileExists = File.Exists(tvBanner.GetFullImagePath());

                    if (fileExists)
                    {
                        if (fanartCount.ContainsKey(tvBanner.SeriesID))
                            fanartCount[tvBanner.SeriesID] = fanartCount[tvBanner.SeriesID] + 1;
                        else
                            fanartCount[tvBanner.SeriesID] = 1;
                    }
                }

                foreach (TvDB_ImageWideBanner tvBanner in allBanners)
                {
                    if (string.IsNullOrEmpty(tvBanner.GetFullImagePath())) continue;
                    bool fileExists = File.Exists(tvBanner.GetFullImagePath());

                    int bannersAvailable = 0;
                    if (fanartCount.ContainsKey(tvBanner.SeriesID))
                        bannersAvailable = fanartCount[tvBanner.SeriesID];

                    if (!fileExists && bannersAvailable < ServerSettings.Instance.TvDB_AutoWideBannersAmount)
                    {
                        CommandRequest_DownloadImage cmd =
                            new CommandRequest_DownloadImage(tvBanner.TvDB_ImageWideBannerID,
                                ImageEntityType.TvDB_Banner, false);
                        cmd.Save();

                        if (fanartCount.ContainsKey(tvBanner.SeriesID))
                            fanartCount[tvBanner.SeriesID] = fanartCount[tvBanner.SeriesID] + 1;
                        else
                            fanartCount[tvBanner.SeriesID] = 1;
                    }
                }
            }

            // TvDB Episodes

            foreach (TvDB_Episode tvEpisode in Repo.TvDB_Episode.GetAll())
            {
                if (string.IsNullOrEmpty(tvEpisode.GetFullImagePath())) continue;
                bool fileExists = File.Exists(tvEpisode.GetFullImagePath());
                if (!fileExists)
                {
                    CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(tvEpisode.TvDB_EpisodeID,
                        ImageEntityType.TvDB_Episode, false);
                    cmd.Save();
                }
            }

            // MovieDB Posters
            if (ServerSettings.Instance.MovieDB_AutoPosters)
            {
                Dictionary<int, int> postersCount = new Dictionary<int, int>();

                // build a dictionary of series and how many images exist
                IReadOnlyList<MovieDB_Poster> allPosters = Repo.MovieDB_Poster.GetAll();
                foreach (MovieDB_Poster moviePoster in allPosters)
                {
                    if (string.IsNullOrEmpty(moviePoster.GetFullImagePath())) continue;
                    bool fileExists = File.Exists(moviePoster.GetFullImagePath());

                    if (fileExists)
                    {
                        if (postersCount.ContainsKey(moviePoster.MovieId))
                            postersCount[moviePoster.MovieId] = postersCount[moviePoster.MovieId] + 1;
                        else
                            postersCount[moviePoster.MovieId] = 1;
                    }
                }

                foreach (MovieDB_Poster moviePoster in allPosters)
                {
                    if (string.IsNullOrEmpty(moviePoster.GetFullImagePath())) continue;
                    bool fileExists = File.Exists(moviePoster.GetFullImagePath());

                    int postersAvailable = 0;
                    if (postersCount.ContainsKey(moviePoster.MovieId))
                        postersAvailable = postersCount[moviePoster.MovieId];

                    if (!fileExists && postersAvailable < ServerSettings.Instance.MovieDB_AutoPostersAmount)
                    {
                        CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(
                            moviePoster.MovieDB_PosterID,
                            ImageEntityType.MovieDB_Poster, false);
                        cmd.Save();

                        if (postersCount.ContainsKey(moviePoster.MovieId))
                            postersCount[moviePoster.MovieId] = postersCount[moviePoster.MovieId] + 1;
                        else
                            postersCount[moviePoster.MovieId] = 1;
                    }
                }
            }

            // MovieDB Fanart
            if (ServerSettings.Instance.MovieDB_AutoFanart)
            {
                Dictionary<int, int> fanartCount = new Dictionary<int, int>();

                // build a dictionary of series and how many images exist
                IReadOnlyList<MovieDB_Fanart> allFanarts = Repo.MovieDB_Fanart.GetAll();
                foreach (MovieDB_Fanart movieFanart in allFanarts)
                {
                    if (string.IsNullOrEmpty(movieFanart.GetFullImagePath())) continue;
                    bool fileExists = File.Exists(movieFanart.GetFullImagePath());

                    if (fileExists)
                    {
                        if (fanartCount.ContainsKey(movieFanart.MovieId))
                            fanartCount[movieFanart.MovieId] = fanartCount[movieFanart.MovieId] + 1;
                        else
                            fanartCount[movieFanart.MovieId] = 1;
                    }
                }

                foreach (MovieDB_Fanart movieFanart in Repo.MovieDB_Fanart.GetAll())
                {
                    if (string.IsNullOrEmpty(movieFanart.GetFullImagePath())) continue;
                    bool fileExists = File.Exists(movieFanart.GetFullImagePath());

                    int fanartAvailable = 0;
                    if (fanartCount.ContainsKey(movieFanart.MovieId))
                        fanartAvailable = fanartCount[movieFanart.MovieId];

                    if (!fileExists && fanartAvailable < ServerSettings.Instance.MovieDB_AutoFanartAmount)
                    {
                        CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(
                            movieFanart.MovieDB_FanartID,
                            ImageEntityType.MovieDB_FanArt, false);
                        cmd.Save();

                        if (fanartCount.ContainsKey(movieFanart.MovieId))
                            fanartCount[movieFanart.MovieId] = fanartCount[movieFanart.MovieId] + 1;
                        else
                            fanartCount[movieFanart.MovieId] = 1;
                    }
                }
            }

            // AniDB Characters
            if (ServerSettings.Instance.AniDB_DownloadCharacters)
            {
                foreach (AniDB_Character chr in Repo.AniDB_Character.GetAll())
                {
                    if (string.IsNullOrEmpty(chr.GetPosterPath())) continue;
                    bool fileExists = File.Exists(chr.GetPosterPath());
                    if (fileExists) continue;
                    var AnimeID = Repo.AniDB_Anime_Character.GetByCharID(chr.CharID)?.FirstOrDefault()
                                      ?.AnimeID ?? 0;
                    if (AnimeID == 0) continue;
                    CommandRequest_DownloadAniDBImages cmd =
                        new CommandRequest_DownloadAniDBImages(AnimeID, false);
                    cmd.Save();
                }
            }

            // AniDB Creators
            if (ServerSettings.Instance.AniDB_DownloadCreators)
            {
                foreach (AniDB_Seiyuu seiyuu in Repo.AniDB_Seiyuu.GetAll())
                {
                    if (string.IsNullOrEmpty(seiyuu.GetPosterPath())) continue;
                    bool fileExists = File.Exists(seiyuu.GetPosterPath());
                    if (fileExists) continue;
                    var chr = Repo.AniDB_Character_Seiyuu.GetBySeiyuuID(seiyuu.SeiyuuID).FirstOrDefault();
                    if (chr == null) continue;
                    var AnimeID = Repo.AniDB_Anime_Character.GetByCharID(chr.CharID)?.FirstOrDefault()
                                      ?.AnimeID ?? 0;
                    if (AnimeID == 0) continue;
                    CommandRequest_DownloadAniDBImages cmd =
                        new CommandRequest_DownloadAniDBImages(AnimeID, false);
                    cmd.Save();
                }
            }
        }

        public static void ValidateAllImages()
        {
            CommandRequest_ValidateAllImages cmd = new CommandRequest_ValidateAllImages();
            cmd.Save();
        }

        public static void RunImport_ScanTvDB()
        {
            TvDBApiHelper.ScanForMatches();
        }

        public static void RunImport_ScanTrakt()
        {
            if (ServerSettings.Instance.Trakt_IsEnabled && !string.IsNullOrEmpty(ServerSettings.Instance.Trakt_AuthToken))
                TraktTVHelper.ScanForMatches();
        }

        public static void RunImport_ScanMovieDB()
        {
            MovieDBHelper.ScanForMatches();
        }

        public static void RunImport_UpdateTvDB(bool forced)
        {
            TvDBApiHelper.UpdateAllInfo(forced);
        }

        public static void RunImport_UpdateAllAniDB()
        {
            foreach (SVR_AniDB_Anime anime in Repo.AniDB_Anime.GetAll())
            {
                CommandRequest_GetAnimeHTTP cmd = new CommandRequest_GetAnimeHTTP(anime.AnimeID, true, false);
                cmd.Save();
            }
        }

        public static void RemoveRecordsWithoutPhysicalFiles()
        {
            logger.Info("Remove Missing Files: Start");
            HashSet<SVR_AnimeEpisode> episodesToUpdate = new HashSet<SVR_AnimeEpisode>();
            HashSet<SVR_AnimeSeries> seriesToUpdate = new HashSet<SVR_AnimeSeries>();
            {
                // remove missing files in valid import folders
                Dictionary<SVR_ImportFolder, List<SVR_VideoLocal_Place>> filesAll = Repo.VideoLocal_Place.GetAll()
                    .Where(a => a.ImportFolder != null)
                    .GroupBy(a => a.ImportFolder)
                    .ToDictionary(a => a.Key, a => a.ToList());
                foreach (SVR_ImportFolder folder in filesAll.Keys)
                {
                    IFileSystem fs = folder.FileSystem;
                    if (fs == null) continue;

                    foreach (SVR_VideoLocal_Place vl in filesAll[folder])
                    {
                        FileSystemResult<IObject> obj = null;
                        if (!string.IsNullOrWhiteSpace(vl.FullServerPath)) obj = (FileSystemResult<IObject>)fs.Resolve(vl.FullServerPath);
                        if (obj != null && obj.Status == Status.Ok) continue;
                        // delete video local record
                        logger.Info("Removing Missing File: {0}", vl.VideoLocalID);
                        vl.RemoveRecordWithOpenTransaction(episodesToUpdate, seriesToUpdate);
                    }
                }

                List<SVR_VideoLocal> videoLocalsAll = Repo.VideoLocal.GetAll().ToList();
                // remove empty videolocals
                Repo.VideoLocal.FindAndDelete(() => videoLocalsAll.Where(a => a.IsEmpty()).ToList());

                // Remove duplicate videolocals
                Dictionary<string, List<SVR_VideoLocal>> locals = videoLocalsAll
                    .Where(a => !string.IsNullOrWhiteSpace(a.Hash))
                    .GroupBy(a => a.Hash)
                    .ToDictionary(g => g.Key, g => g.ToList());
                var toRemove = new List<SVR_VideoLocal>();
                var comparer = new VideoLocalComparer();

                foreach (string hash in locals.Keys)
                {
                    List<SVR_VideoLocal> values = locals[hash];
                    values.Sort(comparer);
                    SVR_VideoLocal to = values.First();
                    List<SVR_VideoLocal> froms = values.Where(s => s != to).ToList();
                    foreach (SVR_VideoLocal from in froms)
                    {
                        List<SVR_VideoLocal_Place> places = from.Places;
                        if (places == null || places.Count == 0) continue;
                        {
                            Repo.VideoLocal_Place.BatchAction(places, places.Count, (place, _) => place.VideoLocalID = to.VideoLocalID);
                        }
                    }
                    toRemove.AddRange(froms);
                }

                Repo.VideoLocal.FindAndDelete(() => toRemove);

                // Remove files in invalid import folders
                foreach (SVR_VideoLocal v in videoLocalsAll)
                {
                    List<SVR_VideoLocal_Place> places = v.Places;
                    if (v.Places?.Count > 0)
                    {
                        foreach (SVR_VideoLocal_Place place in places)
                        {
                            if (!string.IsNullOrWhiteSpace(place?.FullServerPath)) continue;
                            logger.Info("RemoveRecordsWithOrphanedImportFolder : {0}", v.FileName);
                            episodesToUpdate.UnionWith(v.GetAnimeEpisodes());
                            seriesToUpdate.UnionWith(v.GetAnimeEpisodes().Select(a => a.GetAnimeSeries())
                                .DistinctBy(a => a.AnimeSeriesID));
                            Repo.VideoLocal_Place.Delete(place);
                        }
                    }
                    // Remove duplicate places
                    places = v.Places;
                    if (places?.Count == 1) continue;
                    if (places?.Count > 0)
                    {
                        places = places.DistinctBy(a => a.FullServerPath).ToList();
                        places = v.Places?.Except(places).ToList();
                        Repo.VideoLocal_Place.Delete(places);
                    }
                    if (v.Places?.Count > 0) continue;
                    // delete video local record
                    logger.Info("RemoveOrphanedVideoLocal : {0}", v.FileName);
                    episodesToUpdate.UnionWith(v.GetAnimeEpisodes());
                    seriesToUpdate.UnionWith(v.GetAnimeEpisodes().Select(a => a.GetAnimeSeries())
                        .DistinctBy(a => a.AnimeSeriesID));
                    CommandRequest_DeleteFileFromMyList cmdDel =
                        new CommandRequest_DeleteFileFromMyList(v.MyListID);
                    cmdDel.Save();
                    Repo.VideoLocal.Delete(v);
                }

                // Clean up failed imports
                Repo.CrossRef_File_Episode.FindAndDelete(() => Repo.VideoLocal.GetAll().SelectMany(a => Repo.CrossRef_File_Episode.GetByHash(a.Hash))
                    .Where(a => Repo.AniDB_Anime.GetByID(a.AnimeID) == null ||
                                a.GetEpisode() == null).ToList());

                // update everything we modified
                Repo.AnimeEpisode.BatchAction(episodesToUpdate, episodesToUpdate.Count, (ep, _) =>
                    {
                        if (ep.AnimeEpisodeID == 0)
                        {
                            ep.PlexContract = null;
                        }
                        try
                        {
                            ep.PlexContract = Helper.GenerateVideoFromAnimeEpisode(ep);
                        }
                        catch (Exception ex)
                        {
                            LogManager.GetCurrentClassLogger().Error(ex, ex.ToString());
                        }
                    });
                        
                foreach (SVR_AnimeSeries ser in seriesToUpdate)
                {
                    ser.QueueUpdateStats();
                }
            }
            logger.Info("Remove Missing Files: Finished");
        }

        public static string DeleteCloudAccount(int cloudaccountID)
        {
            SVR_CloudAccount cl = Repo.CloudAccount.GetByID(cloudaccountID);
            if (cl == null) return "Could not find Cloud Account ID: " + cloudaccountID;
            foreach (SVR_ImportFolder f in Repo.ImportFolder.GetByCloudId(cl.CloudID))
            {
                string r = DeleteImportFolder(f.ImportFolderID);
                if (!string.IsNullOrEmpty(r))
                    return r;
            }
            Repo.CloudAccount.Delete(cloudaccountID);
            ServerInfo.Instance.RefreshImportFolders();
            ServerInfo.Instance.RefreshCloudAccounts();
            return string.Empty;
        }

        public static string DeleteImportFolder(int importFolderID)
        {
            try
            {
                SVR_ImportFolder ns = Repo.ImportFolder.GetByID(importFolderID);

                if (ns == null) return "Could not find Import Folder ID: " + importFolderID;

                // first delete all the files attached  to this import folder
                Dictionary<int, SVR_AnimeSeries> affectedSeries = new Dictionary<int, SVR_AnimeSeries>();

                foreach (SVR_VideoLocal_Place vid in Repo.VideoLocal_Place.GetByImportFolder(importFolderID))
                {
                    //Thread.Sleep(5000);
                    logger.Info("Deleting video local record: {0}", vid.FullServerPath);

                    List<SVR_AnimeEpisode> animeEpisodes = vid.VideoLocal?.GetAnimeEpisodes();
                    if (animeEpisodes?.Count > 0)
                    {
                        var ser = animeEpisodes[0].GetAnimeSeries();
                        if (ser != null && !affectedSeries.ContainsKey(ser.AnimeSeriesID))
                            affectedSeries.Add(ser.AnimeSeriesID, ser);
                    }
                    SVR_VideoLocal v = vid.VideoLocal;
                    // delete video local record
                    logger.Info("RemoveRecordsWithoutPhysicalFiles : {0}", vid.FullServerPath);
                    if (v?.Places.Count == 1)
                    {
                        Repo.VideoLocal_Place.Delete(vid);
                        Repo.VideoLocal.Delete(v);
                        CommandRequest_DeleteFileFromMyList cmdDel =
                            new CommandRequest_DeleteFileFromMyList(v.MyListID);
                        cmdDel.Save();
                    }
                    else
                        Repo.VideoLocal_Place.Delete(vid);
                }

                // delete any duplicate file records which reference this folder
                Repo.DuplicateFile.Delete(Repo.DuplicateFile.GetByImportFolder1(importFolderID));
                Repo.DuplicateFile.Delete(Repo.DuplicateFile.GetByImportFolder2(importFolderID));

                // delete the import folder
                Repo.ImportFolder.Delete(importFolderID);

                //TODO APIv2: Delete this hack after migration to headless
                //hack until gui id dead
                try
                {
                    Utils.MainThreadDispatch(() =>
                    {
                        ServerInfo.Instance.RefreshImportFolders();
                    });
                }
                catch
                {
                    //dont do this at home :-)
                }

                foreach (SVR_AnimeSeries ser in affectedSeries.Values)
                {
                    ser.QueueUpdateStats();
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public static void UpdateAllStats()
        {
            foreach (SVR_AnimeSeries ser in Repo.AnimeSeries.GetAll())
            {
                ser.QueueUpdateStats();
            }

            foreach (SVR_GroupFilter gf in Repo.GroupFilter.GetAll())
            {
                gf.QueueUpdate();
            }

            Repo.GroupFilter.CreateOrVerifyLockedFilters();
        }

        public static int UpdateAniDBFileData(bool missingInfo, bool outOfDate, bool countOnly)
        {
            List<int> vidsToUpdate = new List<int>();
            try
            {
                if (missingInfo)
                {
                    List<SVR_VideoLocal> vids = Repo.VideoLocal.GetByAniDBResolution("0x0");

                    foreach (SVR_VideoLocal vid in vids)
                    {
                        if (!vidsToUpdate.Contains(vid.VideoLocalID))
                            vidsToUpdate.Add(vid.VideoLocalID);
                    }

                    vids = Repo.VideoLocal.GetWithMissingChapters();
                    foreach (SVR_VideoLocal vid in vids)
                    {
                        if (!vidsToUpdate.Contains(vid.VideoLocalID))
                            vidsToUpdate.Add(vid.VideoLocalID);
                    }
                }

                if (outOfDate)
                {
                    List<SVR_VideoLocal> vids = Repo.VideoLocal.GetByInternalVersion(1);

                    foreach (SVR_VideoLocal vid in vids)
                    {
                        if (!vidsToUpdate.Contains(vid.VideoLocalID))
                            vidsToUpdate.Add(vid.VideoLocalID);
                    }
                }

                if (!countOnly)
                {
                    foreach (int id in vidsToUpdate)
                    {
                        CommandRequest_GetFile cmd = new CommandRequest_GetFile(id, true);
                        cmd.Save();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return vidsToUpdate.Count;
        }

        public static void CheckForDayFilters()
        {
            ScheduledUpdate sched =
                Repo.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.DayFiltersUpdate);
            if (sched != null)
            {
                if (DateTime.Now.Day == sched.LastUpdate.Day)
                    return;
            }
            //Get GroupFiters that change daily

            HashSet<GroupFilterConditionType> conditions = new HashSet<GroupFilterConditionType>
            {
                GroupFilterConditionType.AirDate,
                GroupFilterConditionType.LatestEpisodeAirDate,
                GroupFilterConditionType.SeriesCreatedDate,
                GroupFilterConditionType.EpisodeWatchedDate,
                GroupFilterConditionType.EpisodeAddedDate
            };
            List<SVR_GroupFilter> evalfilters = Repo.GroupFilter.GetWithConditionsTypes(conditions)
                .Where(
                    a => a.Conditions.Any(b => conditions.Contains(b.GetConditionTypeEnum()) &&
                                               b.GetConditionOperatorEnum() == GroupFilterOperator.LastXDays))
                .ToList();

            Repo.GroupFilter.BatchAction(evalfilters, evalfilters.Count, (g, _) => g.CalculateGroupsAndSeries());

            using (var upd = Repo.ScheduledUpdate.BeginAddOrUpdate(() => sched, () =>
            {
                return new ScheduledUpdate
                {
                    UpdateType = (int)ScheduledUpdateType.DayFiltersUpdate,
                    UpdateDetails = string.Empty
                };
            }))
            {
                upd.Entity.LastUpdate = DateTime.Now;
                upd.Commit();
            }
        }

        public static void CheckForTvDBUpdates(bool forceRefresh)
        {
            if (ServerSettings.Instance.TvDB_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.TvDB_UpdateFrequency);

            // update tvdb info every 12 hours

            ScheduledUpdate sched = Repo.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.TvDBInfo);
            if (sched != null)
            {
                // if we have run this in the last 12 hours and are not forcing it, then exit
                TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
                if (tsLastRun.TotalHours < freqHours)
                {
                    if (!forceRefresh) return;
                }
            }

            List<int> tvDBIDs = new List<int>();
            bool tvDBOnline = false;
            string serverTime = TvDBApiHelper.IncrementalTvDBUpdate(ref tvDBIDs, ref tvDBOnline);

            if (tvDBOnline)
            {
                foreach (int tvid in tvDBIDs)
                {
                    // download and update series info, episode info and episode images
                    // will also download fanart, posters and wide banners
                    CommandRequest_TvDBUpdateSeries cmdSeriesEps =
                        new CommandRequest_TvDBUpdateSeries(tvid,
                            true);
                    cmdSeriesEps.Save();
                }
            }

            using (var upd = Repo.ScheduledUpdate.BeginAddOrUpdate(() => sched, () =>
            {
                return new ScheduledUpdate
                {
                    UpdateType = (int)ScheduledUpdateType.TvDBInfo
                };
            }))
            {
                upd.Entity.LastUpdate = DateTime.Now;
                upd.Entity.UpdateDetails = serverTime;
                upd.Commit();
            }

            TvDBApiHelper.ScanForMatches();
        }

        public static void CheckForCalendarUpdate(bool forceRefresh)
        {
            if (ServerSettings.Instance.AniDB_Calendar_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh)
                return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.AniDB_Calendar_UpdateFrequency);

            // update the calendar every 12 hours
            // we will always assume that an anime was downloaded via http first

            ScheduledUpdate sched =
                Repo.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBCalendar);
            if (sched != null)
            {
                // if we have run this in the last 12 hours and are not forcing it, then exit
                TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
                if (tsLastRun.TotalHours < freqHours)
                {
                    if (!forceRefresh) return;
                }
            }

            CommandRequest_GetCalendar cmd = new CommandRequest_GetCalendar(forceRefresh);
            cmd.Save();
        }

        public static void SendUserInfoUpdate(bool forceRefresh)
        {
            // update the anonymous user info every 12 hours
            // we will always assume that an anime was downloaded via http first

            ScheduledUpdate sched =
                Repo.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AzureUserInfo);
            if (sched != null)
            {
                // if we have run this in the last 6 hours and are not forcing it, then exit
                TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
                if (tsLastRun.TotalHours < 6)
                {
                    if (!forceRefresh) return;
                }
            }

            using (var upd = Repo.ScheduledUpdate.BeginAddOrUpdate(() => sched, () =>
            {
                return new ScheduledUpdate
                {
                    UpdateType = (int)ScheduledUpdateType.AzureUserInfo,
                    UpdateDetails = string.Empty
                };
            }))
            {
                upd.Entity.LastUpdate = DateTime.Now;
                upd.Commit();
            }

            CommandRequest_Azure_SendUserInfo cmd =
                new CommandRequest_Azure_SendUserInfo(ServerSettings.Instance.AniDB_Username);
            cmd.Save();
        }

        public static void CheckForAnimeUpdate(bool forceRefresh)
        {
            if (ServerSettings.Instance.AniDB_Anime_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.AniDB_Anime_UpdateFrequency);

            // check for any updated anime info every 12 hours

            ScheduledUpdate sched = Repo.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBUpdates);
            if (sched != null)
            {
                // if we have run this in the last 12 hours and are not forcing it, then exit
                TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
                if (tsLastRun.TotalHours < freqHours)
                {
                    if (!forceRefresh) return;
                }
            }

            CommandRequest_GetUpdated cmd = new CommandRequest_GetUpdated(true);
            cmd.Save();
        }

        public static void CheckForMyListStatsUpdate(bool forceRefresh)
        {
            if (ServerSettings.Instance.AniDB_MyListStats_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh)
                return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.AniDB_MyListStats_UpdateFrequency);

            ScheduledUpdate sched =
                Repo.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBMylistStats);
            if (sched != null)
            {
                // if we have run this in the last 24 hours and are not forcing it, then exit
                TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
                logger.Trace("Last AniDB MyList Stats Update: {0} minutes ago", tsLastRun.TotalMinutes);
                if (tsLastRun.TotalHours < freqHours)
                {
                    if (!forceRefresh) return;
                }
            }

            CommandRequest_UpdateMyListStats cmd = new CommandRequest_UpdateMyListStats(forceRefresh);
            cmd.Save();
        }

        public static void CheckForMyListSyncUpdate(bool forceRefresh)
        {
            if (ServerSettings.Instance.AniDB_MyList_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.AniDB_MyList_UpdateFrequency);

            // update the calendar every 24 hours

            ScheduledUpdate sched =
                Repo.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBMyListSync);
            if (sched != null)
            {
                // if we have run this in the last 24 hours and are not forcing it, then exit
                TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
                logger.Trace("Last AniDB MyList Sync: {0} minutes ago", tsLastRun.TotalMinutes);
                if (tsLastRun.TotalHours < freqHours)
                {
                    if (!forceRefresh) return;
                }
            }

            CommandRequest_SyncMyList cmd = new CommandRequest_SyncMyList(forceRefresh);
            cmd.Save();
        }

        public static void CheckForTraktSyncUpdate(bool forceRefresh)
        {
            if (!ServerSettings.Instance.Trakt_IsEnabled) return;
            if (ServerSettings.Instance.Trakt_SyncFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.Trakt_SyncFrequency);

            // update the calendar every xxx hours

            ScheduledUpdate sched = Repo.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.TraktSync);
            if (sched != null)
            {
                // if we have run this in the last xxx hours and are not forcing it, then exit
                TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
                logger.Trace("Last Trakt Sync: {0} minutes ago", tsLastRun.TotalMinutes);
                if (tsLastRun.TotalHours < freqHours)
                {
                    if (!forceRefresh) return;
                }
            }

            if (ServerSettings.Instance.Trakt_IsEnabled && !string.IsNullOrEmpty(ServerSettings.Instance.Trakt_AuthToken))
            {
                CommandRequest_TraktSyncCollection cmd = new CommandRequest_TraktSyncCollection(false);
                cmd.Save();
            }
        }

        public static void CheckForTraktAllSeriesUpdate(bool forceRefresh)
        {
            if (!ServerSettings.Instance.Trakt_IsEnabled) return;
            if (ServerSettings.Instance.Trakt_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.Trakt_UpdateFrequency);

            // update the calendar every xxx hours
            ScheduledUpdate sched = Repo.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.TraktUpdate);
            if (sched != null)
            {
                // if we have run this in the last xxx hours and are not forcing it, then exit
                TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
                logger.Trace("Last Trakt Update: {0} minutes ago", tsLastRun.TotalMinutes);
                if (tsLastRun.TotalHours < freqHours)
                {
                    if (!forceRefresh) return;
                }
            }

            CommandRequest_TraktUpdateAllSeries cmd = new CommandRequest_TraktUpdateAllSeries(false);
            cmd.Save();
        }

        public static void CheckForTraktTokenUpdate(bool forceRefresh)
        {
            try
            {
                if (!ServerSettings.Instance.Trakt_IsEnabled) return;
                // by updating the Trakt token regularly, the user won't need to authorize again
                int freqHours = 24; // we need to update this daily

                ScheduledUpdate sched =
                    Repo.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.TraktToken);
                if (sched != null)
                {
                    // if we have run this in the last xxx hours and are not forcing it, then exit
                    TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
                    logger.Trace("Last Trakt Token Update: {0} minutes ago", tsLastRun.TotalMinutes);
                    if (tsLastRun.TotalHours < freqHours)
                    {
                        if (!forceRefresh) return;
                    }
                }

                TraktTVHelper.RefreshAuthToken();

                using (var upd = Repo.ScheduledUpdate.BeginAddOrUpdate(() => sched, () =>
                {
                    return new ScheduledUpdate
                    {
                        UpdateType = (int)ScheduledUpdateType.TraktToken,
                        UpdateDetails = string.Empty
                    };
                }))
                {
                    upd.Entity.LastUpdate = DateTime.Now;
                    upd.Commit();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in CheckForTraktTokenUpdate: " + ex.ToString());
            }
        }

        public static void CheckForAniDBFileUpdate(bool forceRefresh)
        {
            if (ServerSettings.Instance.AniDB_File_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.AniDB_File_UpdateFrequency);

            // check for any updated anime info every 12 hours

            ScheduledUpdate sched =
                Repo.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBFileUpdates);
            if (sched != null)
            {
                // if we have run this in the last 12 hours and are not forcing it, then exit
                TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
                if (tsLastRun.TotalHours < freqHours && !forceRefresh)
                {
                    return;
                }
            }

            UpdateAniDBFileData(true, false, false);

            // files which have been hashed, but don't have an associated episode
            List<SVR_VideoLocal> filesWithoutEpisode = Repo.VideoLocal.GetVideosWithoutEpisode();

            foreach (SVR_VideoLocal vl in filesWithoutEpisode)
            {
                CommandRequest_ProcessFile cmd = new CommandRequest_ProcessFile(vl.VideoLocalID, true);
                cmd.Save();
            }

            // now check for any files which have been manually linked and are less than 30 days old

            using (var upd = Repo.ScheduledUpdate.BeginAddOrUpdate(() => sched, () =>
            {
                return new ScheduledUpdate
                {
                    UpdateType = (int)ScheduledUpdateType.AniDBFileUpdates,
                    UpdateDetails = string.Empty
                };
            }))
            {
                upd.Entity.LastUpdate = DateTime.Now;
                upd.Commit();
            }
        }

        public static void CheckForPreviouslyIgnored()
        {
            try
            {
                IReadOnlyList<SVR_VideoLocal> filesAll = Repo.VideoLocal.GetAll();
                IReadOnlyList<SVR_VideoLocal> filesIgnored = Repo.VideoLocal.GetIgnoredVideos();

                foreach (SVR_VideoLocal vl in filesAll)
                {
                    if (vl.IsIgnored == 0)
                    {
                        // Check if we have this file marked as previously ignored, matches only if it has the same hash
                        List<SVR_VideoLocal> resultVideoLocalsIgnored =
                            filesIgnored.Where(s => s.Hash == vl.Hash).ToList();

                        if (resultVideoLocalsIgnored.Any())
                        {
                            using (var upd = Repo.VideoLocal.BeginAddOrUpdate(() => vl))
                            {
                                upd.Entity.IsIgnored = 1;
                                upd.Commit(false);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, string.Format("Error in CheckForPreviouslyIgnored: {0}", ex));
            }
        }

        public static void UpdateAniDBTitles()
        {
            int freqHours = 100;

            bool process = false;

            if (!process) return;

            // check for any updated anime info every 100 hours

            ScheduledUpdate sched = Repo.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBTitles);
            if (sched != null)
            {
                // if we have run this in the last 100 hours and are not forcing it, then exit
                TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
                if (tsLastRun.TotalHours < freqHours) return;
            }

            using (var upd = Repo.ScheduledUpdate.BeginAddOrUpdate(() => sched, () =>
            {
                return new ScheduledUpdate
                {
                    UpdateType = (int)ScheduledUpdateType.AniDBTitles,
                    UpdateDetails = string.Empty
                };
            }))
            {
                upd.Entity.LastUpdate = DateTime.Now;
                upd.Commit();
            }

            CommandRequest_GetAniDBTitles cmd = new CommandRequest_GetAniDBTitles();
            cmd.Save();
        }
    }
}