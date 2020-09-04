using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentNHibernate.Utils;
using NLog;
using NutzCode.CloudFileSystem;
using Shoko.Commons.Extensions;
using Shoko.Commons.Queue;
using Shoko.Models.Azure;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Commands.Azure;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.FileHelper;
using Shoko.Server.Models;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Utils = Shoko.Server.Utilities.Utils;

namespace Shoko.Server
{
    public class Importer
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static void RunImport_IntegrityCheck()
        {
            // files which have not been hashed yet
            // or files which do not have a VideoInfo record
            List<SVR_VideoLocal> filesToHash = RepoFactory.VideoLocal.GetVideosWithoutHash();
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
            foreach (SVR_VideoLocal v in RepoFactory.VideoLocal.GetVideosWithoutEpisode()
                .Where(a => !string.IsNullOrEmpty(a.Hash)))
            {
                CommandRequest_ProcessFile cmd = new CommandRequest_ProcessFile(v.VideoLocalID, false);
                cmd.Save();
            }


            // check that all the episode data is populated
            foreach (SVR_VideoLocal vl in RepoFactory.VideoLocal.GetAll().Where(a => !string.IsNullOrEmpty(a.Hash)))
            {
                // if the file is not manually associated, then check for AniDB_File info
                SVR_AniDB_File aniFile = RepoFactory.AniDB_File.GetByHash(vl.Hash);
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
                    AniDB_Episode ep = RepoFactory.AniDB_Episode.GetByEpisodeID(xref.EpisodeID);
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
            if (!ServerSettings.Instance.WebCache.Enabled) return; 
            List<SVR_VideoLocal> allfiles = RepoFactory.VideoLocal.GetAll().ToList();
            AzureWebAPI.Send_Media(allfiles);
        }

        public static void SyncHashes()
        {
            if (!ServerSettings.Instance.WebCache.Enabled) return; 
            bool paused = ShokoService.CmdProcessorHasher.Paused;
            ShokoService.CmdProcessorHasher.Paused = true;
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                List<SVR_VideoLocal> allfiles = RepoFactory.VideoLocal.GetAll().ToList();
                List<SVR_VideoLocal> missfiles = allfiles.Where(
                        a =>
                            string.IsNullOrEmpty(a.CRC32) || string.IsNullOrEmpty(a.SHA1) ||
                            string.IsNullOrEmpty(a.MD5) || a.SHA1 == "0000000000000000000000000000000000000000" ||
                            a.MD5 == "00000000000000000000000000000000")
                    .ToList();
                List<SVR_VideoLocal> withfiles = allfiles.Except(missfiles).ToList();
                //Check if we can populate md5,sha and crc from AniDB_Files
                foreach (SVR_VideoLocal v in missfiles.ToList())
                {
                    ShokoService.CmdProcessorHasher.QueueState = new QueueStateStruct
                    {
                        queueState = QueueStateEnum.CheckingFile,
                        extraParams = new[] {v.FileName}
                    };
                    SVR_AniDB_File file = RepoFactory.AniDB_File.GetByHash(v.ED2KHash);
                    if (file != null)
                    {
                        if (!string.IsNullOrEmpty(file.CRC) && !string.IsNullOrEmpty(file.SHA1) &&
                            !string.IsNullOrEmpty(file.MD5))
                        {
                            v.CRC32 = file.CRC;
                            v.MD5 = file.MD5;
                            v.SHA1 = file.SHA1;
                            RepoFactory.VideoLocal.Save(v, false);
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
                            v.CRC32 = ls[0].CRC32.ToUpperInvariant();
                            v.MD5 = ls[0].MD5.ToUpperInvariant();
                            v.SHA1 = ls[0].SHA1.ToUpperInvariant();
                            RepoFactory.VideoLocal.Save(v, false);
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
                        SVR_VideoLocal_Place p = v.GetBestVideoLocalPlace(true);
                        if (p != null && p.ImportFolder.CloudID == 0)
                        {
                            ShokoService.CmdProcessorHasher.QueueState = new QueueStateStruct
                            {
                                queueState = QueueStateEnum.HashingFile,
                                extraParams = new[] {v.FileName}
                            };
                            Hashes h = FileHashHelper.GetHashInfo(p.FullServerPath, true, ShokoServer.OnHashProgress,
                                true,
                                true,
                                true);

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
                //Send the hashes
                AzureWebAPI.Send_FileHash(withfiles);
                logger.Info("Sync Hashes Complete");
            }
            ShokoService.CmdProcessorHasher.Paused = paused;
        }


        public static void RunImport_ScanFolder(int importFolderID, bool skipMyList = false)
        {
            // get a complete list of files
            List<string> fileList = new List<string>();
            int filesFound = 0, videosFound = 0;
            int i = 0;

            try
            {
                SVR_ImportFolder fldr = RepoFactory.ImportFolder.GetByID(importFolderID);
                if (fldr == null) return;

                // first build a list of files that we already know about, as we don't want to process them again


                List<SVR_VideoLocal_Place> filesAll =
                    RepoFactory.VideoLocalPlace.GetByImportFolder(fldr.ImportFolderID);
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
                            ex);
                        logger.Info(msg);
                    }
                }


                logger.Debug("ImportFolder: {0} || {1}", fldr.ImportFolderName, fldr.ImportFolderLocation);
                Utils.GetFilesForImportFolder(fldr.BaseDirectory, ref fileList);

                // Get Ignored Files and remove them from the scan listing
                var ignoredFiles = RepoFactory.VideoLocal.GetIgnoredVideos().SelectMany(a => a.Places)
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

                    if (ServerSettings.Instance.Import.Exclude.Any(s => fileName.Contains(s)))
                    {
                        logger.Trace("Import exclusion, skipping --- {0}", fileName);
                        continue;
                    }

                    filesFound++;
                    logger.Trace("Processing File {0}/{1} --- {2}", i, fileList.Count, fileName);

                    if (!FileHashHelper.IsVideo(fileName)) continue;

                    videosFound++;

                    new CommandRequest_HashFile(fileName, false, skipMyList).Save();
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
            foreach (SVR_ImportFolder share in RepoFactory.ImportFolder.GetAll())
            {
                if (!share.FolderIsDropSource) continue;

                logger.Debug("ImportFolder: {0} || {1}", share.ImportFolderName, share.ImportFolderLocation);
                Utils.GetFilesForImportFolder(share.BaseDirectory, ref fileList);
            }

            // Get Ignored Files and remove them from the scan listing
            var ignoredFiles = RepoFactory.VideoLocal.GetIgnoredVideos().SelectMany(a => a.Places)
                .Select(a => a.FullServerPath).Where(a => !string.IsNullOrEmpty(a)).ToList();
            fileList = fileList.Except(ignoredFiles, StringComparer.InvariantCultureIgnoreCase).ToList();

            // get a list of all the shares we are looking at
            int filesFound = 0, videosFound = 0;
            int i = 0;

            // get a list of all files in the share
            foreach (string fileName in fileList)
            {
                i++;

                if (ServerSettings.Instance.Import.Exclude.Any(s => fileName.Contains(s)))
                {
                    logger.Trace("Import exclusion, skipping --- {0}", fileName);
                    continue;
                }
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
            IReadOnlyList<SVR_VideoLocal_Place> filesAll = RepoFactory.VideoLocalPlace.GetAll();
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
                        ex);
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
            foreach (SVR_ImportFolder share in RepoFactory.ImportFolder.GetAll())
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
                if (ServerSettings.Instance.Import.Exclude.Any(s => fileName.Contains(s)))
                {
                    logger.Trace("Import exclusion, skipping --- {0}", fileName);
                    continue;
                }
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
            List<SVR_VideoLocal_Place> filesAll = RepoFactory.VideoLocalPlace.GetByImportFolder(fldr.ImportFolderID);
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
                if (ServerSettings.Instance.Import.Exclude.Any(s => fileName.Contains(s)))
                {
                    logger.Trace("Import exclusion, skipping --- {0}", fileName);
                    continue;
                } 

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
            foreach (SVR_AniDB_Anime anime in RepoFactory.AniDB_Anime.GetAll())
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
            if (ServerSettings.Instance.TvDB.AutoPosters)
            {
                Dictionary<int, int> postersCount = new Dictionary<int, int>();

                // build a dictionary of series and how many images exist
                IReadOnlyList<TvDB_ImagePoster> allPosters = RepoFactory.TvDB_ImagePoster.GetAll();
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

                    if (!fileExists && postersAvailable < ServerSettings.Instance.TvDB.AutoPostersAmount)
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
            if (ServerSettings.Instance.TvDB.AutoFanart)
            {
                Dictionary<int, int> fanartCount = new Dictionary<int, int>();
                IReadOnlyList<TvDB_ImageFanart> allFanart = RepoFactory.TvDB_ImageFanart.GetAll();
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

                    if (!fileExists && fanartAvailable < ServerSettings.Instance.TvDB.AutoFanartAmount)
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
            if (ServerSettings.Instance.TvDB.AutoWideBanners)
            {
                Dictionary<int, int> fanartCount = new Dictionary<int, int>();

                // build a dictionary of series and how many images exist
                IReadOnlyList<TvDB_ImageWideBanner> allBanners = RepoFactory.TvDB_ImageWideBanner.GetAll();
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

                    if (!fileExists && bannersAvailable < ServerSettings.Instance.TvDB.AutoWideBannersAmount)
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

            foreach (TvDB_Episode tvEpisode in RepoFactory.TvDB_Episode.GetAll())
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
            if (ServerSettings.Instance.MovieDb.AutoPosters)
            {
                Dictionary<int, int> postersCount = new Dictionary<int, int>();

                // build a dictionary of series and how many images exist
                IReadOnlyList<MovieDB_Poster> allPosters = RepoFactory.MovieDB_Poster.GetAll();
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

                    if (!fileExists && postersAvailable < ServerSettings.Instance.MovieDb.AutoPostersAmount)
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
            if (ServerSettings.Instance.MovieDb.AutoFanart)
            {
                Dictionary<int, int> fanartCount = new Dictionary<int, int>();

                // build a dictionary of series and how many images exist
                IReadOnlyList<MovieDB_Fanart> allFanarts = RepoFactory.MovieDB_Fanart.GetAll();
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

                foreach (MovieDB_Fanart movieFanart in RepoFactory.MovieDB_Fanart.GetAll())
                {
                    if (string.IsNullOrEmpty(movieFanart.GetFullImagePath())) continue;
                    bool fileExists = File.Exists(movieFanart.GetFullImagePath());

                    int fanartAvailable = 0;
                    if (fanartCount.ContainsKey(movieFanart.MovieId))
                        fanartAvailable = fanartCount[movieFanart.MovieId];

                    if (!fileExists && fanartAvailable < ServerSettings.Instance.MovieDb.AutoFanartAmount)
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
            if (ServerSettings.Instance.AniDb.DownloadCharacters)
            {
                foreach (AniDB_Character chr in RepoFactory.AniDB_Character.GetAll())
                {
                    if (string.IsNullOrEmpty(chr.GetPosterPath())) continue;
                    bool fileExists = File.Exists(chr.GetPosterPath());
                    if (fileExists) continue;
                    var AnimeID = RepoFactory.AniDB_Anime_Character.GetByCharID(chr.CharID)?.FirstOrDefault()
                                      ?.AnimeID ?? 0;
                    if (AnimeID == 0) continue;
                    CommandRequest_DownloadAniDBImages cmd =
                        new CommandRequest_DownloadAniDBImages(AnimeID, false);
                    cmd.Save();
                }
            }

            // AniDB Creators
            if (ServerSettings.Instance.AniDb.DownloadCreators)
            {
                foreach (AniDB_Seiyuu seiyuu in RepoFactory.AniDB_Seiyuu.GetAll())
                {
                    if (string.IsNullOrEmpty(seiyuu.GetPosterPath())) continue;
                    bool fileExists = File.Exists(seiyuu.GetPosterPath());
                    if (fileExists) continue;
                    var chr = RepoFactory.AniDB_Character_Seiyuu.GetBySeiyuuID(seiyuu.SeiyuuID).FirstOrDefault();
                    if (chr == null) continue;
                    var AnimeID = RepoFactory.AniDB_Anime_Character.GetByCharID(chr.CharID)?.FirstOrDefault()
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
            Analytics.PostEvent("Management", nameof(ValidateAllImages));

            CommandRequest_ValidateAllImages cmd = new CommandRequest_ValidateAllImages();
            cmd.Save();
        }

        public static void RunImport_ScanTvDB()
        {
            Analytics.PostEvent("Management", nameof(RunImport_ScanTvDB));

            TvDBApiHelper.ScanForMatches();
        }

        public static void RunImport_ScanTrakt()
        {
            if (ServerSettings.Instance.TraktTv.Enabled && !string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
                TraktTVHelper.ScanForMatches();
        }

        public static void RunImport_ScanMovieDB()
        {
            Analytics.PostEvent("Management", nameof(RunImport_ScanMovieDB));
            MovieDBHelper.ScanForMatches();
        }

        public static void RunImport_UpdateTvDB(bool forced)
        {
            Analytics.PostEvent("Management", nameof(RunImport_UpdateTvDB));

            TvDBApiHelper.UpdateAllInfo(forced);
        }

        public static void RunImport_UpdateAllAniDB()
        {
            Analytics.PostEvent("Management", nameof(RunImport_UpdateAllAniDB));

            foreach (SVR_AniDB_Anime anime in RepoFactory.AniDB_Anime.GetAll())
            {
                CommandRequest_GetAnimeHTTP cmd = new CommandRequest_GetAnimeHTTP(anime.AnimeID, true, false);
                cmd.Save();
            }
        }

        public static void RemoveRecordsWithoutPhysicalFiles(bool removeMyList = true)
        {
            logger.Info("Remove Missing Files: Start");
            HashSet<SVR_AnimeSeries> seriesToUpdate = new HashSet<SVR_AnimeSeries>();
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                // remove missing files in valid import folders
                Dictionary<SVR_ImportFolder, List<SVR_VideoLocal_Place>> filesAll = RepoFactory.VideoLocalPlace.GetAll()
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
                        if (!string.IsNullOrWhiteSpace(vl.FullServerPath)) obj = fs.Resolve(vl.FullServerPath);
                        if (obj != null && obj.IsOk) continue;
                        // delete video local record
                        logger.Info("Removing Missing File: {0}", vl.VideoLocalID);
                        vl.RemoveRecordWithOpenTransaction(session, seriesToUpdate);
                    }
                }

                List<SVR_VideoLocal> videoLocalsAll = RepoFactory.VideoLocal.GetAll().ToList();
                // remove empty videolocals
                using (var transaction = session.BeginTransaction())
                {
                    foreach (SVR_VideoLocal remove in videoLocalsAll.Where(a => a.IsEmpty()).ToList())
                    {
                        RepoFactory.VideoLocal.DeleteWithOpenTransaction(session, remove);
                    }
                    transaction.Commit();
                }
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
                    List<SVR_VideoLocal> froms = values.Except(to).ToList();
                    foreach (SVR_VideoLocal from in froms)
                    {
                        List<SVR_VideoLocal_Place> places = from.Places;
                        if (places == null || places.Count == 0) continue;
                        using (var transaction = session.BeginTransaction())
                        {
                            foreach (SVR_VideoLocal_Place place in places)
                            {
                                place.VideoLocalID = to.VideoLocalID;
                                RepoFactory.VideoLocalPlace.SaveWithOpenTransaction(session, place);
                            }
                            transaction.Commit();
                        }
                    }
                    toRemove.AddRange(froms);
                }

                using (var transaction = session.BeginTransaction())
                {
                    foreach (SVR_VideoLocal remove in toRemove)
                    {
                        RepoFactory.VideoLocal.DeleteWithOpenTransaction(session, remove);
                    }
                    transaction.Commit();
                }

                // Remove files in invalid import folders
                foreach (SVR_VideoLocal v in videoLocalsAll)
                {
                    List<SVR_VideoLocal_Place> places = v.Places;
                    if (v.Places?.Count > 0)
                    {
                        using (var transaction = session.BeginTransaction())
                        {
                            foreach (SVR_VideoLocal_Place place in places)
                            {
                                if (!string.IsNullOrWhiteSpace(place?.FullServerPath)) continue;
                                logger.Info("RemoveRecordsWithOrphanedImportFolder : {0}", v.FileName);
                                seriesToUpdate.UnionWith(v.GetAnimeEpisodes().Select(a => a.GetAnimeSeries())
                                    .DistinctBy(a => a.AnimeSeriesID));
                                RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, place);
                            }
                            transaction.Commit();
                        }
                    }
                    // Remove duplicate places
                    places = v.Places;
                    if (places?.Count == 1) continue;
                    if (places?.Count > 0)
                    {
                        places = places.DistinctBy(a => a.FullServerPath).ToList();
                        places = v.Places?.Except(places).ToList();
                        foreach (SVR_VideoLocal_Place place in places)
                        {
                            using (var transaction = session.BeginTransaction())
                            {
                                RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, place);
                                transaction.Commit();
                            }
                        }
                    }
                    if (v.Places?.Count > 0) continue;
                    // delete video local record
                    logger.Info("RemoveOrphanedVideoLocal : {0}", v.FileName);
                    seriesToUpdate.UnionWith(v.GetAnimeEpisodes().Select(a => a.GetAnimeSeries())
                        .DistinctBy(a => a.AnimeSeriesID));

                    if (removeMyList)
                    {
                        CommandRequest_DeleteFileFromMyList cmdDel =
                            new CommandRequest_DeleteFileFromMyList(v.MyListID);
                        cmdDel.Save();
                    }

                    using (var transaction = session.BeginTransaction())
                    {
                        RepoFactory.VideoLocal.DeleteWithOpenTransaction(session, v);
                        transaction.Commit();
                    }
                }

                // Clean up failed imports
                using (var transaction = session.BeginTransaction())
                {
                    var list = RepoFactory.VideoLocal.GetAll().SelectMany(a => RepoFactory.CrossRef_File_Episode.GetByHash(a.Hash))
                        .Where(a => RepoFactory.AniDB_Anime.GetByAnimeID(a.AnimeID) == null ||
                                    a.GetEpisode() == null).ToArray();
                    foreach (var xref in list)
                    {
                        // We don't need to update anything since they don't exist
                        RepoFactory.CrossRef_File_Episode.DeleteWithOpenTransaction(session, xref);
                    }
                    transaction.Commit();
                }

                // update everything we modified
                foreach (SVR_AnimeSeries ser in seriesToUpdate)
                {
                    ser.QueueUpdateStats();
                }
            }
            logger.Info("Remove Missing Files: Finished");
        }

        public static string DeleteCloudAccount(int cloudaccountID)
        {
            SVR_CloudAccount cl = RepoFactory.CloudAccount.GetByID(cloudaccountID);
            if (cl == null) return "Could not find Cloud Account ID: " + cloudaccountID;
            foreach (SVR_ImportFolder f in RepoFactory.ImportFolder.GetByCloudId(cl.CloudID))
            {
                string r = DeleteImportFolder(f.ImportFolderID);
                if (!string.IsNullOrEmpty(r))
                    return r;
            }
            RepoFactory.CloudAccount.Delete(cloudaccountID);
            ServerInfo.Instance.RefreshImportFolders();
            ServerInfo.Instance.RefreshCloudAccounts();
            return string.Empty;
        }

        public static string DeleteImportFolder(int importFolderID, bool removeFromMyList = true)
        {
            try
            {
                SVR_ImportFolder ns = RepoFactory.ImportFolder.GetByID(importFolderID);
                if (ns == null) return "Could not find Import Folder ID: " + importFolderID;

                HashSet<SVR_AnimeSeries> affectedSeries = new HashSet<SVR_AnimeSeries>();
                var vids = RepoFactory.VideoLocalPlace.GetByImportFolder(importFolderID);
                logger.Info($"Deleting {vids.Count} video local records");
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                vids.ForEach(vid =>
                    vid.RemoveRecordWithOpenTransaction(session, affectedSeries, removeFromMyList, false));

                // delete any duplicate file records which reference this folder
                RepoFactory.DuplicateFile.Delete(RepoFactory.DuplicateFile.GetByImportFolder1(importFolderID));
                RepoFactory.DuplicateFile.Delete(RepoFactory.DuplicateFile.GetByImportFolder2(importFolderID));

                // delete the import folder
                RepoFactory.ImportFolder.Delete(importFolderID);

                foreach (SVR_AnimeSeries ser in affectedSeries)
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
            Analytics.PostEvent("Management", "Update All Stats");
            foreach (SVR_AnimeSeries ser in RepoFactory.AnimeSeries.GetAll())
            {
                ser.QueueUpdateStats();
            }

            foreach (SVR_GroupFilter gf in RepoFactory.GroupFilter.GetAll())
            {
                gf.QueueUpdate();
            }

            CommandRequest_RefreshGroupFilter cmd = new CommandRequest_RefreshGroupFilter(0);
            cmd.Save();
        }


        public static int UpdateAniDBFileData(bool missingInfo, bool outOfDate, bool countOnly)
        {
            List<int> vidsToUpdate = new List<int>();
            try
            {
                if (missingInfo)
                {
                    List<SVR_VideoLocal> vids = RepoFactory.VideoLocal.GetByAniDBResolution("0x0");

                    foreach (SVR_VideoLocal vid in vids)
                    {
                        if (!vidsToUpdate.Contains(vid.VideoLocalID))
                            vidsToUpdate.Add(vid.VideoLocalID);
                    }

                    vids = RepoFactory.VideoLocal.GetWithMissingChapters();
                    foreach (SVR_VideoLocal vid in vids)
                    {
                        if (!vidsToUpdate.Contains(vid.VideoLocalID))
                            vidsToUpdate.Add(vid.VideoLocalID);
                    }
                }

                if (outOfDate)
                {
                    List<SVR_VideoLocal> vids = RepoFactory.VideoLocal.GetByInternalVersion(1);

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
                RepoFactory.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.DayFiltersUpdate);
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
            List<SVR_GroupFilter> evalfilters = RepoFactory.GroupFilter.GetWithConditionsTypes(conditions)
                .Where(
                    a => a.Conditions.Any(b => conditions.Contains(b.GetConditionTypeEnum()) &&
                                               b.GetConditionOperatorEnum() == GroupFilterOperator.LastXDays))
                .ToList();
            foreach (SVR_GroupFilter g in evalfilters)
                g.CalculateGroupsAndSeries();
            RepoFactory.GroupFilter.Save(evalfilters);

            if (sched == null)
            {
                sched = new ScheduledUpdate
                {
                    UpdateDetails = string.Empty,
                    UpdateType = (int)ScheduledUpdateType.DayFiltersUpdate
                };
            }

            sched.LastUpdate = DateTime.Now;
            RepoFactory.ScheduledUpdate.Save(sched);
        }


        public static void CheckForTvDBUpdates(bool forceRefresh)
        {
            if (ServerSettings.Instance.TvDB.UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.TvDB.UpdateFrequency);

            // update tvdb info every 12 hours

            ScheduledUpdate sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.TvDBInfo);
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

            if (sched == null)
            {
                sched = new ScheduledUpdate
                {
                    UpdateType = (int)ScheduledUpdateType.TvDBInfo
                };
            }

            sched.LastUpdate = DateTime.Now;
            sched.UpdateDetails = serverTime;
            RepoFactory.ScheduledUpdate.Save(sched);

            TvDBApiHelper.ScanForMatches();
        }

        public static void CheckForCalendarUpdate(bool forceRefresh)
        {
            if (ServerSettings.Instance.AniDb.Calendar_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh)
                return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.AniDb.Calendar_UpdateFrequency);

            // update the calendar every 12 hours
            // we will always assume that an anime was downloaded via http first


            ScheduledUpdate sched =
                RepoFactory.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBCalendar);
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
            if (!ServerSettings.Instance.WebCache.Enabled) return; 
            // update the anonymous user info every 12 hours
            // we will always assume that an anime was downloaded via http first

            ScheduledUpdate sched =
                RepoFactory.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AzureUserInfo);
            if (sched != null)
            {
                // if we have run this in the last 6 hours and are not forcing it, then exit
                TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
                if (tsLastRun.TotalHours < 6)
                {
                    if (!forceRefresh) return;
                }
            }

            if (sched == null)
            {
                sched = new ScheduledUpdate
                {
                    UpdateType = (int)ScheduledUpdateType.AzureUserInfo,
                    UpdateDetails = string.Empty
                };
            }
            sched.LastUpdate = DateTime.Now;
            RepoFactory.ScheduledUpdate.Save(sched);

            CommandRequest_Azure_SendUserInfo cmd =
                new CommandRequest_Azure_SendUserInfo(ServerSettings.Instance.AniDb.Username);
            cmd.Save();
        }

        public static void CheckForAnimeUpdate(bool forceRefresh)
        {
            if (ServerSettings.Instance.AniDb.Anime_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.AniDb.Anime_UpdateFrequency);

            // check for any updated anime info every 12 hours

            ScheduledUpdate sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBUpdates);
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
            if (ServerSettings.Instance.AniDb.MyListStats_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh)
                return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.AniDb.MyListStats_UpdateFrequency);

            ScheduledUpdate sched =
                RepoFactory.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBMylistStats);
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
            if (ServerSettings.Instance.AniDb.MyList_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.AniDb.MyList_UpdateFrequency);

            // update the calendar every 24 hours

            ScheduledUpdate sched =
                RepoFactory.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBMyListSync);
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
            if (!ServerSettings.Instance.TraktTv.Enabled) return;
            if (ServerSettings.Instance.TraktTv.SyncFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.TraktTv.SyncFrequency);

            // update the calendar every xxx hours

            ScheduledUpdate sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.TraktSync);
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

            if (ServerSettings.Instance.TraktTv.Enabled && !string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
            {
                CommandRequest_TraktSyncCollection cmd = new CommandRequest_TraktSyncCollection(false);
                cmd.Save();
            }
        }

        public static void CheckForTraktAllSeriesUpdate(bool forceRefresh)
        {
            if (!ServerSettings.Instance.TraktTv.Enabled) return;
            if (ServerSettings.Instance.TraktTv.UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.TraktTv.UpdateFrequency);

            // update the calendar every xxx hours
            ScheduledUpdate sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.TraktUpdate);
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
                if (!ServerSettings.Instance.TraktTv.Enabled) return;
                // by updating the Trakt token regularly, the user won't need to authorize again
                int freqHours = 24; // we need to update this daily

                ScheduledUpdate sched =
                    RepoFactory.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.TraktToken);
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
                if (sched == null)
                {
                    sched = new ScheduledUpdate
                    {
                        UpdateType = (int)ScheduledUpdateType.TraktToken,
                        UpdateDetails = string.Empty
                    };
                }
                sched.LastUpdate = DateTime.Now;
                RepoFactory.ScheduledUpdate.Save(sched);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in CheckForTraktTokenUpdate: " + ex);
            }
        }

        public static void CheckForAniDBFileUpdate(bool forceRefresh)
        {
            if (ServerSettings.Instance.AniDb.File_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.AniDb.File_UpdateFrequency);

            // check for any updated anime info every 12 hours

            ScheduledUpdate sched =
                RepoFactory.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBFileUpdates);
            if (sched != null)
            {
                // if we have run this in the last 12 hours and are not forcing it, then exit
                TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
                if (tsLastRun.TotalHours < freqHours)
                {
                    if (!forceRefresh) return;
                }
            }

            UpdateAniDBFileData(true, false, false);

            // files which have been hashed, but don't have an associated episode
            List<SVR_VideoLocal> filesWithoutEpisode = RepoFactory.VideoLocal.GetVideosWithoutEpisode();

            foreach (SVR_VideoLocal vl in filesWithoutEpisode)
            {
                CommandRequest_ProcessFile cmd = new CommandRequest_ProcessFile(vl.VideoLocalID, true);
                cmd.Save();
            }

            // now check for any files which have been manually linked and are less than 30 days old


            if (sched == null)
            {
                sched = new ScheduledUpdate
                {
                    UpdateType = (int)ScheduledUpdateType.AniDBFileUpdates,
                    UpdateDetails = string.Empty
                };
            }
            sched.LastUpdate = DateTime.Now;
            RepoFactory.ScheduledUpdate.Save(sched);
        }

        public static void CheckForPreviouslyIgnored()
        {
            try
            {
                IReadOnlyList<SVR_VideoLocal> filesAll = RepoFactory.VideoLocal.GetAll();
                IReadOnlyList<SVR_VideoLocal> filesIgnored = RepoFactory.VideoLocal.GetIgnoredVideos();

                foreach (SVR_VideoLocal vl in filesAll)
                {
                    if (vl.IsIgnored == 0)
                    {
                        // Check if we have this file marked as previously ignored, matches only if it has the same hash
                        List<SVR_VideoLocal> resultVideoLocalsIgnored =
                            filesIgnored.Where(s => s.Hash == vl.Hash).ToList();

                        if (resultVideoLocalsIgnored.Any())
                        {
                            vl.IsIgnored = 1;
                            RepoFactory.VideoLocal.Save(vl, false);
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

            ScheduledUpdate sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBTitles);
            if (sched != null)
            {
                // if we have run this in the last 100 hours and are not forcing it, then exit
                TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
                if (tsLastRun.TotalHours < freqHours) return;
            }

            if (sched == null)
            {
                sched = new ScheduledUpdate
                {
                    UpdateType = (int)ScheduledUpdateType.AniDBTitles,
                    UpdateDetails = string.Empty
                };
            }
            sched.LastUpdate = DateTime.Now;
            RepoFactory.ScheduledUpdate.Save(sched);

            CommandRequest_GetAniDBTitles cmd = new CommandRequest_GetAniDBTitles();
            cmd.Save();
        }
    }
}
