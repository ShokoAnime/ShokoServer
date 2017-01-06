﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JMMContracts;
using JMMServer.Commands;
using JMMServer.Commands.AniDB;
using JMMServer.Commands.Azure;
using JMMServer.Databases;
using JMMServer.Entities;
using JMMServer.FileHelper;
 using JMMServer.PlexAndKodi;
 using JMMServer.Providers.Azure;
using JMMServer.Providers.MovieDB;
using JMMServer.Providers.MyAnimeList;
using JMMServer.Providers.TraktTV;
using JMMServer.Providers.TvDB;
using JMMServer.Repositories;
using JMMServer.Repositories.Cached;
using JMMServer.Repositories.Direct;
using JMMServer.Repositories.NHibernate;
 using Nancy.Extensions;
 using NLog;
using NutzCode.CloudFileSystem;
using NutzCode.CloudFileSystem.Plugins.LocalFileSystem;
using CrossRef_File_Episode = JMMServer.Entities.CrossRef_File_Episode;

using File = Pri.LongPath.File;

namespace JMMServer
{
    public class Importer
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static void RunImport_IntegrityCheck()
        {

            /*
            // files which don't have a valid import folder
            List<VideoLocal> filesToDelete = RepoFactory.VideoLocal.GetVideosWithoutImportFolder();
            foreach (VideoLocal vl in filesToDelete)
                RepoFactory.VideoLocal.Delete(vl.VideoLocalID);
            */

            // files which have not been hashed yet
            // or files which do not have a VideoInfo record
            List<VideoLocal> filesToHash = RepoFactory.VideoLocal.GetVideosWithoutHash();
            Dictionary<int, VideoLocal> dictFilesToHash = new Dictionary<int, VideoLocal>();
            foreach (VideoLocal vl in filesToHash)
            {
                dictFilesToHash[vl.VideoLocalID] = vl;
                VideoLocal_Place p = vl.GetBestVideoLocalPlace();
                if (p != null)
                {
                    CommandRequest_HashFile cmd = new CommandRequest_HashFile(p.FullServerPath, false);
                    cmd.Save();
                }
            }

            List<VideoLocal> filesToRehash = RepoFactory.VideoLocal.GetVideosWithoutVideoInfo();
            Dictionary<int, VideoLocal> dictFilesToRehash = new Dictionary<int, VideoLocal>();
            foreach (VideoLocal vl in filesToHash)
            {
                dictFilesToRehash[vl.VideoLocalID] = vl;
                // don't use if it is in the previous list
                if (!dictFilesToHash.ContainsKey(vl.VideoLocalID))
                {
                    try
                    {
                        VideoLocal_Place p = vl.GetBestVideoLocalPlace();
                        if (p != null)
                        {
                            CommandRequest_HashFile cmd = new CommandRequest_HashFile(p.FullServerPath, false);
                            cmd.Save();
                        }
                    }
                    catch (Exception ex)
                    {
                        string msg = string.Format("Error RunImport_IntegrityCheck XREF: {0} - {1}",
                            vl.ToStringDetailed(), ex.ToString());
                        logger.Info(msg);
                    }
                }
            }

            // files which have been hashed, but don't have an associated episode
            foreach (VideoLocal v in RepoFactory.VideoLocal.GetVideosWithoutEpisode().Where(a => !string.IsNullOrEmpty(a.Hash)))
            {
                CommandRequest_ProcessFile cmd = new CommandRequest_ProcessFile(v.VideoLocalID, false);
                cmd.Save();
                continue;
            }




            // check that all the episode data is populated
            foreach (VideoLocal vl in RepoFactory.VideoLocal.GetAll().Where(a => !string.IsNullOrEmpty(a.Hash)))
            {
                // if the file is not manually associated, then check for AniDB_File info
                AniDB_File aniFile = RepoFactory.AniDB_File.GetByHash(vl.Hash);
                foreach (CrossRef_File_Episode xref in vl.EpisodeCrossRefs)
                {
                    if (xref.CrossRefSource != (int)CrossRefSource.AniDB) continue;
                    if (aniFile == null)
                    {
                        CommandRequest_ProcessFile cmd = new CommandRequest_ProcessFile(vl.VideoLocalID, false);
                        cmd.Save();
                        continue;
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
                    continue;
                }
            }
        }

        public static void SyncMedia()
        {
            List<VideoLocal> allfiles = RepoFactory.VideoLocal.GetAll().ToList();
            AzureWebAPI.Send_Media(allfiles);
        }

        public static void SyncHashes()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                List<VideoLocal> allfiles = RepoFactory.VideoLocal.GetAll().ToList();
                List<VideoLocal> missfiles = allfiles.Where(
                            a =>
                                string.IsNullOrEmpty(a.CRC32) || string.IsNullOrEmpty(a.SHA1) ||
                                string.IsNullOrEmpty(a.MD5) || a.SHA1 == "0000000000000000000000000000000000000000" || a.MD5 == "00000000000000000000000000000000")
                        .ToList();
                List<VideoLocal> withfiles = allfiles.Except(missfiles).ToList();
                //Check if we can populate md5,sha and crc from AniDB_Files
                foreach (VideoLocal v in missfiles.ToList())
                {
                    AniDB_File file = RepoFactory.AniDB_File.GetByHash(v.ED2KHash);
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
                        }
                    }
                }
                //Try obtain missing hashes
                foreach (VideoLocal v in missfiles.ToList())
                {
                    List<FileHash> ls = AzureWebAPI.Get_FileHash(FileHashType.ED2K, v.ED2KHash);
                    if (ls != null)
                    {
                        ls = ls.Where(
                            a =>
                                !string.IsNullOrEmpty(a.CRC32) && !string.IsNullOrEmpty(a.MD5) &&
                                !string.IsNullOrEmpty(a.SHA1)).ToList();
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
                List<VideoLocal> tosend = new List<VideoLocal>();
                foreach (VideoLocal v in missfiles)
                {
                    try
                    {
                        VideoLocal_Place p = v.GetBestVideoLocalPlace();
                        if (p != null && p.ImportFolder.CloudID == 0)
                        {
                            Hashes h = FileHashHelper.GetHashInfo(p.FullServerPath, true, MainWindow.OnHashProgress, true,
                                true,
                                true);

                            v.Hash = h.ed2k;
                            v.CRC32 = h.crc32;
                            v.MD5 = h.md5;
                            v.SHA1 = h.sha1;
                            v.HashSource = (int)HashSource.DirectHash;
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
        }


        public static void RunImport_ScanFolder(int importFolderID)
        {
            // get a complete list of files
            List<string> fileList = new List<string>();
            int filesFound = 0, videosFound = 0;
            int i = 0;

            try
            {
                ImportFolder fldr = RepoFactory.ImportFolder.GetByID(importFolderID);
                if (fldr == null) return;

                // first build a list of files that we already know about, as we don't want to process them again


                List<VideoLocal_Place> filesAll = RepoFactory.VideoLocalPlace.GetByImportFolder(fldr.ImportFolderID);
                Dictionary<string, VideoLocal_Place> dictFilesExisting = new Dictionary<string, VideoLocal_Place>();
                foreach (VideoLocal_Place vl in filesAll)
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

                // get a list of all files in the share
                foreach (string fileName in fileList)
                {
                    i++;

                    if (dictFilesExisting.ContainsKey(fileName))
                    {
                        if (fldr.IsDropSource == 1)
                            dictFilesExisting[fileName].MoveFileIfRequired();
                    }

                    filesFound++;
                    logger.Info("Processing File {0}/{1} --- {2}", i, fileList.Count, fileName);

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
            foreach (ImportFolder share in RepoFactory.ImportFolder.GetAll())
            {
                if (!share.FolderIsDropSource) continue;

                logger.Debug("ImportFolder: {0} || {1}", share.ImportFolderName, share.ImportFolderLocation);
                Utils.GetFilesForImportFolder(share.BaseDirectory, ref fileList);
            }

            // get a list of all the shares we are looking at
            int filesFound = 0, videosFound = 0;
            int i = 0;

            // get a list of all files in the share
            foreach (string fileName in fileList)
            {
                i++;
                filesFound++;
                logger.Info("Processing File {0}/{1} --- {2}", i, fileList.Count, fileName);

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
            IReadOnlyList<VideoLocal_Place> filesAll = RepoFactory.VideoLocalPlace.GetAll();
            Dictionary<string, VideoLocal_Place> dictFilesExisting = new Dictionary<string, VideoLocal_Place>();
            foreach (VideoLocal_Place vl in filesAll)
            {
                try
                {
	                if (vl.FullServerPath == null)
	                {
		                logger.Info("Invalid File Path found. Removing: " + vl.VideoLocal_Place_ID);
		                RepoFactory.VideoLocalPlace.Delete(vl);
		                continue;
	                }
	                dictFilesExisting[vl.FullServerPath] = vl;
                }
                catch (Exception ex)
                {
                    string msg = string.Format("Error RunImport_NewFiles XREF: {0} - {1}", ((vl.FullServerPath ?? vl.FilePath) ?? vl.VideoLocal_Place_ID.ToString()),
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
            foreach (ImportFolder share in RepoFactory.ImportFolder.GetAll())
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
                logger.Info("Processing File {0}/{1} --- {2}", i, fileList.Count, fileName);

                if (!FileHashHelper.IsVideo(fileName)) continue;

                videosFound++;

                CommandRequest_HashFile cr_hashfile = new CommandRequest_HashFile(fileName, false);
                cr_hashfile.Save();
            }
            logger.Debug("Found {0} files", filesFound);
            logger.Debug("Found {0} videos", videosFound);
        }
        public static void RunImport_ImportFolderNewFiles(ImportFolder fldr)
        {
            List<string> fileList = new List<string>();
            int filesFound = 0, videosFound = 0;
            int i = 0;
            List<VideoLocal_Place> filesAll = RepoFactory.VideoLocalPlace.GetByImportFolder(fldr.ImportFolderID);
            Utils.GetFilesForImportFolder(fldr.BaseDirectory, ref fileList);

            HashSet<string> fs = new HashSet<string>(fileList);
            foreach (VideoLocal_Place v in filesAll)
            {
                if (fs.Contains(v.FullServerPath))
                    fileList.Remove(v.FullServerPath);
            }


            // get a list of all files in the share
            foreach (string fileName in fileList)
            {
                i++;
                filesFound++;
                logger.Info("Processing File {0}/{1} --- {2}", i, fileList.Count, fileName);

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
            foreach (AniDB_Anime anime in RepoFactory.AniDB_Anime.GetAll())
            {
                if (anime.AnimeID == 8580)
                    Console.Write("");

                if (string.IsNullOrEmpty(anime.PosterPath)) continue;

                bool fileExists = File.Exists(anime.PosterPath);
                if (!fileExists)
                {
                    CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(anime.AniDB_AnimeID,
                        JMMImageType.AniDB_Cover,
                        false);
                    cmd.Save();
                }
            }

            // TvDB Posters
            if (ServerSettings.TvDB_AutoPosters)
            {
                Dictionary<int, int> postersCount = new Dictionary<int, int>();

                // build a dictionary of series and how many images exist
                IReadOnlyList<TvDB_ImagePoster> allPosters = RepoFactory.TvDB_ImagePoster.GetAll();
                foreach (TvDB_ImagePoster tvPoster in allPosters)
                {
                    if (string.IsNullOrEmpty(tvPoster.FullImagePath)) continue;
                    bool fileExists = File.Exists(tvPoster.FullImagePath);

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
                    if (string.IsNullOrEmpty(tvPoster.FullImagePath)) continue;
                    bool fileExists = File.Exists(tvPoster.FullImagePath);

                    int postersAvailable = 0;
                    if (postersCount.ContainsKey(tvPoster.SeriesID))
                        postersAvailable = postersCount[tvPoster.SeriesID];

                    if (!fileExists && postersAvailable < ServerSettings.TvDB_AutoPostersAmount)
                    {
                        CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(tvPoster.TvDB_ImagePosterID,
                            JMMImageType.TvDB_Cover, false);
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
                Dictionary<int, int> fanartCount = new Dictionary<int, int>();
                IReadOnlyList<TvDB_ImageFanart> allFanart = RepoFactory.TvDB_ImageFanart.GetAll();
                foreach (TvDB_ImageFanart tvFanart in allFanart)
                {
                    // build a dictionary of series and how many images exist
                    if (string.IsNullOrEmpty(tvFanart.FullImagePath)) continue;
                    bool fileExists = File.Exists(tvFanart.FullImagePath);

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
                    if (string.IsNullOrEmpty(tvFanart.FullImagePath)) continue;
                    bool fileExists = File.Exists(tvFanart.FullImagePath);

                    int fanartAvailable = 0;
                    if (fanartCount.ContainsKey(tvFanart.SeriesID))
                        fanartAvailable = fanartCount[tvFanart.SeriesID];

                    if (!fileExists && fanartAvailable < ServerSettings.TvDB_AutoFanartAmount)
                    {
                        CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(tvFanart.TvDB_ImageFanartID,
                            JMMImageType.TvDB_FanArt, false);
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
                Dictionary<int, int> fanartCount = new Dictionary<int, int>();

                // build a dictionary of series and how many images exist
                IReadOnlyList<TvDB_ImageWideBanner> allBanners = RepoFactory.TvDB_ImageWideBanner.GetAll();
                foreach (TvDB_ImageWideBanner tvBanner in allBanners)
                {
                    if (string.IsNullOrEmpty(tvBanner.FullImagePath)) continue;
                    bool fileExists = File.Exists(tvBanner.FullImagePath);

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
                    if (string.IsNullOrEmpty(tvBanner.FullImagePath)) continue;
                    bool fileExists = File.Exists(tvBanner.FullImagePath);

                    int bannersAvailable = 0;
                    if (fanartCount.ContainsKey(tvBanner.SeriesID))
                        bannersAvailable = fanartCount[tvBanner.SeriesID];

                    if (!fileExists && bannersAvailable < ServerSettings.TvDB_AutoWideBannersAmount)
                    {
                        CommandRequest_DownloadImage cmd =
                            new CommandRequest_DownloadImage(tvBanner.TvDB_ImageWideBannerID,
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

            foreach (TvDB_Episode tvEpisode in RepoFactory.TvDB_Episode.GetAll())
            {
                if (string.IsNullOrEmpty(tvEpisode.FullImagePath)) continue;
                bool fileExists = File.Exists(tvEpisode.FullImagePath);
                if (!fileExists)
                {
                    CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(tvEpisode.TvDB_EpisodeID,
                        JMMImageType.TvDB_Episode, false);
                    cmd.Save();
                }
            }

            // MovieDB Posters
            if (ServerSettings.MovieDB_AutoPosters)
            {
                Dictionary<int, int> postersCount = new Dictionary<int, int>();

                // build a dictionary of series and how many images exist
                IReadOnlyList<MovieDB_Poster> allPosters = RepoFactory.MovieDB_Poster.GetAll();
                foreach (MovieDB_Poster moviePoster in allPosters)
                {
                    if (string.IsNullOrEmpty(moviePoster.FullImagePath)) continue;
                    bool fileExists = File.Exists(moviePoster.FullImagePath);

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
                    if (string.IsNullOrEmpty(moviePoster.FullImagePath)) continue;
                    bool fileExists = File.Exists(moviePoster.FullImagePath);

                    int postersAvailable = 0;
                    if (postersCount.ContainsKey(moviePoster.MovieId))
                        postersAvailable = postersCount[moviePoster.MovieId];

                    if (!fileExists && postersAvailable < ServerSettings.MovieDB_AutoPostersAmount)
                    {
                        CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(
                            moviePoster.MovieDB_PosterID,
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
                Dictionary<int, int> fanartCount = new Dictionary<int, int>();

                // build a dictionary of series and how many images exist
                IReadOnlyList<MovieDB_Fanart> allFanarts = RepoFactory.MovieDB_Fanart.GetAll();
                foreach (MovieDB_Fanart movieFanart in allFanarts)
                {
                    if (string.IsNullOrEmpty(movieFanart.FullImagePath)) continue;
                    bool fileExists = File.Exists(movieFanart.FullImagePath);

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
                    if (string.IsNullOrEmpty(movieFanart.FullImagePath)) continue;
                    bool fileExists = File.Exists(movieFanart.FullImagePath);

                    int fanartAvailable = 0;
                    if (fanartCount.ContainsKey(movieFanart.MovieId))
                        fanartAvailable = fanartCount[movieFanart.MovieId];

                    if (!fileExists && fanartAvailable < ServerSettings.MovieDB_AutoFanartAmount)
                    {
                        CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(
                            movieFanart.MovieDB_FanartID,
                            JMMImageType.MovieDB_FanArt, false);
                        cmd.Save();

                        if (fanartCount.ContainsKey(movieFanart.MovieId))
                            fanartCount[movieFanart.MovieId] = fanartCount[movieFanart.MovieId] + 1;
                        else
                            fanartCount[movieFanart.MovieId] = 1;
                    }
                }
            }

            /*
            // Trakt Posters
            if (ServerSettings.Trakt_DownloadPosters)
            {
                foreach (Trakt_ImagePoster traktPoster in RepoFactory.Trakt_ImagePoster.GetAll())
                {
                    if (string.IsNullOrEmpty(traktPoster.FullImagePath)) continue;
                    bool fileExists = File.Exists(traktPoster.FullImagePath);
                    if (!fileExists)
                    {
                        CommandRequest_DownloadImage cmd =
                            new CommandRequest_DownloadImage(traktPoster.Trakt_ImagePosterID,
                                JMMImageType.Trakt_Poster, false);
                        cmd.Save();
                    }
                }
            }

            // Trakt Fanart
            if (ServerSettings.Trakt_DownloadFanart)
            {
                foreach (Trakt_ImageFanart traktFanart in RepoFactory.Trakt_ImageFanart.GetAll())
                {
                    if (string.IsNullOrEmpty(traktFanart.FullImagePath)) continue;
                    bool fileExists = File.Exists(traktFanart.FullImagePath);
                    if (!fileExists)
                    {
                        CommandRequest_DownloadImage cmd =
                            new CommandRequest_DownloadImage(traktFanart.Trakt_ImageFanartID,
                                JMMImageType.Trakt_Fanart, false);
                        cmd.Save();
                    }
                }
            }

            // Trakt Episode
            if (ServerSettings.Trakt_DownloadEpisodes)
            {
                foreach (Trakt_Episode traktEp in RepoFactory.Trakt_Episode.GetAll())
                {
                    if (string.IsNullOrEmpty(traktEp.FullImagePath)) continue;
                    if (!traktEp.TraktID.HasValue) continue; // if it doesn't have a TraktID it means it is old data

                    bool fileExists = File.Exists(traktEp.FullImagePath);
                    if (!fileExists)
                    {
                        CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(traktEp.Trakt_EpisodeID,
                            JMMImageType.Trakt_Episode, false);
                        cmd.Save();
                    }
                }
            }
            */

            // AniDB Characters
            if (ServerSettings.AniDB_DownloadCharacters)
            {
                foreach (AniDB_Character chr in RepoFactory.AniDB_Character.GetAll())
                {
                    if (chr.CharID == 75250)
                    {
                        Console.WriteLine("test");
                    }

                    if (string.IsNullOrEmpty(chr.PosterPath)) continue;
                    bool fileExists = File.Exists(chr.PosterPath);
                    if (!fileExists)
                    {
                        CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(chr.AniDB_CharacterID,
                            JMMImageType.AniDB_Character, false);
                        cmd.Save();
                    }
                }
            }

            // AniDB Creators
            if (ServerSettings.AniDB_DownloadCreators)
            {
                foreach (AniDB_Seiyuu seiyuu in RepoFactory.AniDB_Seiyuu.GetAll())
                {
                    if (string.IsNullOrEmpty(seiyuu.PosterPath)) continue;
                    bool fileExists = File.Exists(seiyuu.PosterPath);
                    if (!fileExists)
                    {
                        CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(seiyuu.AniDB_SeiyuuID,
                            JMMImageType.AniDB_Creator, false);
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
            foreach (AniDB_Anime anime in RepoFactory.AniDB_Anime.GetAll())
            {
                CommandRequest_GetAnimeHTTP cmd = new CommandRequest_GetAnimeHTTP(anime.AnimeID, true, false);
                cmd.Save();
            }
        }

        public static void RemoveRecordsWithoutPhysicalFiles()
        {
	        using (var session = DatabaseFactory.SessionFactory.OpenSession())
	        {
		        List<AnimeEpisode> toUpdate = new List<AnimeEpisode>();
		        // get a full list of files
		        Dictionary<ImportFolder, List<VideoLocal_Place>> filesAll = RepoFactory.VideoLocalPlace.GetAll()
			        .Where(a => a.ImportFolder != null)
			        .GroupBy(a => a.ImportFolder)
			        .ToDictionary(a => a.Key, a => a.ToList());
		        foreach (ImportFolder folder in filesAll.Keys)
		        {
			        IFileSystem fs = folder.FileSystem;

			        foreach (VideoLocal_Place vl in filesAll[folder])
			        {
				        FileSystemResult<IObject> obj = fs.Resolve(vl.FullServerPath);
				        if (obj.IsOk && !(obj.Result is IDirectory)) continue;
				        VideoLocal v = vl.VideoLocal;
				        // delete video local record
				        logger.Info("RemoveRecordsWithoutPhysicalFiles : {0}", vl.FullServerPath);
				        if (v.Places.Count <= 1)
				        {
					        toUpdate.AddRange(v.GetAnimeEpisodes());
					        RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, vl);
					        RepoFactory.VideoLocal.DeleteWithOpenTransaction(session, v);
					        CommandRequest_DeleteFileFromMyList cmdDel = new CommandRequest_DeleteFileFromMyList(v.Hash, v.FileSize);
					        cmdDel.Save();
				        }
				        else
				        {
					        toUpdate.AddRange(v.GetAnimeEpisodes());
					        RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, vl);
				        }
			        }
		        }

		        IReadOnlyList<VideoLocal> videoLocalsAll = RepoFactory.VideoLocal.GetAll();
		        foreach (VideoLocal v in videoLocalsAll)
		        {
			        if (v.Places?.Count > 0)
			        {
				        foreach (VideoLocal_Place place in v.Places)
				        {
					        if (place.ImportFolder != null) continue;
					        logger.Info("RemoveRecordsWithOrphanedImportFolder : {0}", v.FileName);
					        toUpdate.AddRange(v.GetAnimeEpisodes());
					        RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(session, place);
				        }
			        }
			        if (v.Places?.Count > 0) continue;
			        // delete video local record
			        logger.Info("RemoveOrphanedVideoLocal : {0}", v.FileName);
			        toUpdate.AddRange(v.GetAnimeEpisodes());
			        RepoFactory.VideoLocal.DeleteWithOpenTransaction(session, v);
			        CommandRequest_DeleteFileFromMyList cmdDel = new CommandRequest_DeleteFileFromMyList(v.Hash, v.FileSize);
			        cmdDel.Save();
		        }

		        toUpdate = toUpdate.DistinctBy(a => a.AnimeEpisodeID).ToList();
		        foreach (AnimeEpisode ep in toUpdate)
		        {
			        if (ep.AnimeEpisodeID == 0)
			        {
				        ep.PlexContract = null;
				        RepoFactory.AnimeEpisode.SaveWithOpenTransaction(session, ep);
			        }
			        try
			        {
				        ep.PlexContract = Helper.GenerateVideoFromAnimeEpisode(ep);
				        RepoFactory.AnimeEpisode.SaveWithOpenTransaction(session, ep);
			        }
			        catch (Exception ex)
			        {
				        LogManager.GetCurrentClassLogger().Error(ex, ex.ToString());
			        }
		        }
	        }

	        UpdateAllStats();
        }

        public static string DeleteCloudAccount(int cloudaccountID)
        {
            CloudAccount cl = RepoFactory.CloudAccount.GetByID(cloudaccountID);
            if (cl == null) return "Could not find Cloud Account ID: " + cloudaccountID;
            foreach (ImportFolder f in RepoFactory.ImportFolder.GetByCloudId(cl.CloudID))
            {
                string r = DeleteImportFolder(f.ImportFolderID);
                if (!string.IsNullOrEmpty(r))
                    return r;
            }
            return string.Empty;
        }

        public static string DeleteImportFolder(int importFolderID)
        {
            try
            {
                ImportFolder ns = RepoFactory.ImportFolder.GetByID(importFolderID);

                if (ns == null) return "Could not find Import Folder ID: " + importFolderID;

                // first delete all the files attached  to this import folder
                Dictionary<int, AnimeSeries> affectedSeries = new Dictionary<int, AnimeSeries>();

                foreach (VideoLocal_Place vid in RepoFactory.VideoLocalPlace.GetByImportFolder(importFolderID))
                {
                    //Thread.Sleep(5000);
                    logger.Info("Deleting video local record: {0}", vid.FullServerPath);

                    AnimeSeries ser = null;
                    List<AnimeEpisode> animeEpisodes = vid.VideoLocal.GetAnimeEpisodes();
                    if (animeEpisodes.Count > 0)
                    {
                        ser = animeEpisodes[0].GetAnimeSeries();
                        if (ser != null && !affectedSeries.ContainsKey(ser.AnimeSeriesID))
                            affectedSeries.Add(ser.AnimeSeriesID, ser);
                    }
                    VideoLocal v = vid.VideoLocal;
                    // delete video local record
                    logger.Info("RemoveRecordsWithoutPhysicalFiles : {0}", vid.FullServerPath);
                    if (v.Places.Count == 1)
                    {
                        RepoFactory.VideoLocalPlace.Delete(vid);
                        RepoFactory.VideoLocal.Delete(v);
                        CommandRequest_DeleteFileFromMyList cmdDel = new CommandRequest_DeleteFileFromMyList(v.Hash, v.FileSize);
                        cmdDel.Save();

                    }
                    else
                        RepoFactory.VideoLocalPlace.Delete(vid);
                }

                // delete any duplicate file records which reference this folder
                RepoFactory.DuplicateFile.Delete(RepoFactory.DuplicateFile.GetByImportFolder1(importFolderID));
                RepoFactory.DuplicateFile.Delete(RepoFactory.DuplicateFile.GetByImportFolder2(importFolderID));

                // delete the import folder
                RepoFactory.ImportFolder.Delete(importFolderID);

                //TODO APIv2: Delete this hack after migration to headless
                //hack until gui id dead
                try
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        ServerInfo.Instance.RefreshImportFolders();
                    });
                }
                catch
                {
                    //dont do this at home :-)
                }

                foreach (AnimeSeries ser in affectedSeries.Values)
                {
                    ser.QueueUpdateStats();
                    //StatsCache.Instance.UpdateUsingSeries(ser.AnimeSeriesID);
                }


                return "";
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public static void UpdateAllStats()
        {
            foreach (AnimeSeries ser in RepoFactory.AnimeSeries.GetAll())
            {
                ser.QueueUpdateStats();
            }

	        foreach (GroupFilter gf in RepoFactory.GroupFilter.GetAll())
	        {
		        gf.QueueUpdate();
	        }
        }


        public static int UpdateAniDBFileData(bool missingInfo, bool outOfDate, bool countOnly)
        {
            List<int> vidsToUpdate = new List<int>();
            try
            {

                if (missingInfo)
                {
                    List<VideoLocal> vids = RepoFactory.VideoLocal.GetByAniDBResolution("0x0");

                    foreach (VideoLocal vid in vids)
                    {
                        if (!vidsToUpdate.Contains(vid.VideoLocalID))
                            vidsToUpdate.Add(vid.VideoLocalID);
                    }
                }

                if (outOfDate)
                {
                    List<VideoLocal> vids = RepoFactory.VideoLocal.GetByInternalVersion(1);

                    foreach (VideoLocal vid in vids)
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
            ScheduledUpdate sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.DayFiltersUpdate);
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
            List<GroupFilter> evalfilters = RepoFactory.GroupFilter.GetWithConditionsTypes(conditions).Where(
                a => a.Conditions.Any(b => conditions.Contains(b.ConditionTypeEnum) && b.ConditionOperatorEnum == GroupFilterOperator.LastXDays)).ToList();
            foreach (GroupFilter g in evalfilters)
                g.EvaluateAnimeGroups();
            if (sched == null)
            {
                sched = new ScheduledUpdate();
                sched.UpdateDetails = "";
                sched.UpdateType = (int)ScheduledUpdateType.DayFiltersUpdate;
            }

            sched.LastUpdate = DateTime.Now;
            RepoFactory.ScheduledUpdate.Save(sched);
        }


        public static void CheckForTvDBUpdates(bool forceRefresh)
        {
            if (ServerSettings.TvDB_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.TvDB_UpdateFrequency);

            // update tvdb info every 12 hours

            ScheduledUpdate sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TvDBInfo);
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
            string serverTime = JMMService.TvdbHelper.IncrementalTvDBUpdate(ref tvDBIDs, ref tvDBOnline);

            if (tvDBOnline)
            {
                foreach (int tvid in tvDBIDs)
                {
                    // download and update series info, episode info and episode images
                    // will also download fanart, posters and wide banners
                    CommandRequest_TvDBUpdateSeriesAndEpisodes cmdSeriesEps =
                        new CommandRequest_TvDBUpdateSeriesAndEpisodes(tvid,
                            false);
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
            RepoFactory.ScheduledUpdate.Save(sched);

            TvDBHelper.ScanForMatches();
        }

        public static void CheckForCalendarUpdate(bool forceRefresh)
        {
            if (ServerSettings.AniDB_Calendar_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh)
                return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_Calendar_UpdateFrequency);

            // update the calendar every 12 hours
            // we will always assume that an anime was downloaded via http first


            ScheduledUpdate sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBCalendar);
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

            ScheduledUpdate sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AzureUserInfo);
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
                sched = new ScheduledUpdate();
                sched.UpdateType = (int)ScheduledUpdateType.AzureUserInfo;
                sched.UpdateDetails = "";
            }
            sched.LastUpdate = DateTime.Now;
            RepoFactory.ScheduledUpdate.Save(sched);

            CommandRequest_Azure_SendUserInfo cmd = new CommandRequest_Azure_SendUserInfo(ServerSettings.AniDB_Username);
            cmd.Save();
        }

        public static void CheckForAnimeUpdate(bool forceRefresh)
        {
            if (ServerSettings.AniDB_Anime_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_Anime_UpdateFrequency);

            // check for any updated anime info every 12 hours

            ScheduledUpdate sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBUpdates);
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

        public static void CheckForMALUpdate(bool forceRefresh)
        {
            if (ServerSettings.AniDB_Anime_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.MAL_UpdateFrequency);

            // check for any updated anime info every 12 hours

            ScheduledUpdate sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.MALUpdate);
            if (sched != null)
            {
                // if we have run this in the last 12 hours and are not forcing it, then exit
                TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
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
            RepoFactory.ScheduledUpdate.Save(sched);
        }

        public static void CheckForMyListStatsUpdate(bool forceRefresh)
        {
            if (ServerSettings.AniDB_MyListStats_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh)
                return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_MyListStats_UpdateFrequency);

            ScheduledUpdate sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBMylistStats);
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

            CommandRequest_UpdateMylistStats cmd = new CommandRequest_UpdateMylistStats(forceRefresh);
            cmd.Save();
        }

        public static void CheckForMyListSyncUpdate(bool forceRefresh)
        {
            if (ServerSettings.AniDB_MyList_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_MyList_UpdateFrequency);

            // update the calendar every 24 hours

            ScheduledUpdate sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBMyListSync);
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
            if (ServerSettings.Trakt_SyncFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.Trakt_SyncFrequency);

            // update the calendar every xxx hours

            ScheduledUpdate sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TraktSync);
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

            if (ServerSettings.Trakt_IsEnabled && !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
            {
                CommandRequest_TraktSyncCollection cmd = new CommandRequest_TraktSyncCollection(false);
                cmd.Save();
            }
        }

        public static void CheckForTraktAllSeriesUpdate(bool forceRefresh)
        {
            if (ServerSettings.Trakt_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.Trakt_UpdateFrequency);

            // update the calendar every xxx hours
            ScheduledUpdate sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TraktUpdate);
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
                // by updating the Trakt token regularly, the user won't need to authorize again
                int freqHours = 24; // we need to update this daily

                ScheduledUpdate sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TraktToken);
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
                    sched = new ScheduledUpdate();
                    sched.UpdateType = (int)ScheduledUpdateType.TraktToken;
                    sched.UpdateDetails = "";
                }
                sched.LastUpdate = DateTime.Now;
                RepoFactory.ScheduledUpdate.Save(sched);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in CheckForTraktTokenUpdate: " + ex.ToString());
            }
        }

        public static void CheckForAniDBFileUpdate(bool forceRefresh)
        {
            if (ServerSettings.AniDB_File_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
            int freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_File_UpdateFrequency);

            // check for any updated anime info every 12 hours

            ScheduledUpdate sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBFileUpdates);
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
            List<VideoLocal> filesWithoutEpisode = RepoFactory.VideoLocal.GetVideosWithoutEpisode();

            foreach (VideoLocal vl in filesWithoutEpisode)
            {
                CommandRequest_ProcessFile cmd = new CommandRequest_ProcessFile(vl.VideoLocalID, true);
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
            RepoFactory.ScheduledUpdate.Save(sched);
        }

        public static void CheckForPreviouslyIgnored()
        {
            try
            {
                IReadOnlyList<VideoLocal> filesAll = RepoFactory.VideoLocal.GetAll();
                IReadOnlyList<VideoLocal> filesIgnored = RepoFactory.VideoLocal.GetIgnoredVideos();

                foreach (VideoLocal vl in filesAll)
                {
                    if (vl.IsIgnored == 0)
                    {
                        // Check if we have this file marked as previously ignored, matches only if it has the same hash
                        List<VideoLocal> resultVideoLocalsIgnored = filesIgnored.Where(s => s.Hash == vl.Hash).ToList();

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

            bool process =
                ServerSettings.AniDB_Username.Equals("jonbaby", StringComparison.InvariantCultureIgnoreCase) ||
                ServerSettings.AniDB_Username.Equals("jmediamanager", StringComparison.InvariantCultureIgnoreCase);

            if (!process) return;

            // check for any updated anime info every 100 hours

            ScheduledUpdate sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBTitles);
            if (sched != null)
            {
                // if we have run this in the last 100 hours and are not forcing it, then exit
                TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
                if (tsLastRun.TotalHours < freqHours) return;
            }

            if (sched == null)
            {
                sched = new ScheduledUpdate();
                sched.UpdateType = (int)ScheduledUpdateType.AniDBTitles;
                sched.UpdateDetails = "";
            }
            sched.LastUpdate = DateTime.Now;
            RepoFactory.ScheduledUpdate.Save(sched);

            CommandRequest_GetAniDBTitles cmd = new CommandRequest_GetAniDBTitles();
            cmd.Save();
        }
    }
}