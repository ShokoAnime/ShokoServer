using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentNHibernate.Utils;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Commands.Import;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.FileHelper;
using Shoko.Server.Models;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using Utils = Shoko.Server.Utilities.Utils;

namespace Shoko.Server;

public static class Importer
{
    private static readonly Logger Logger;

    static Importer()
    {
        Logger = LogManager.GetCurrentClassLogger();
    }

    public static void RunImport_IntegrityCheck()
    {
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        // files which have not been hashed yet
        // or files which do not have a VideoInfo record
        var filesToHash = RepoFactory.VideoLocal.GetVideosWithoutHash();
        var dictFilesToHash = new Dictionary<int, SVR_VideoLocal>();
        foreach (var vl in filesToHash)
        {
            dictFilesToHash[vl.VideoLocalID] = vl;
            var p = vl.GetBestVideoLocalPlace(true);
            if (p == null)
            {
                continue;
            }

            commandFactory.CreateAndSave<CommandRequest_HashFile>(c => c.FileName = p.FullServerPath);
        }

        foreach (var vl in filesToHash)
        {
            // don't use if it is in the previous list
            if (dictFilesToHash.ContainsKey(vl.VideoLocalID))
            {
                continue;
            }

            try
            {
                var p = vl.GetBestVideoLocalPlace(true);
                if (p != null)
                {
                    commandFactory.CreateAndSave<CommandRequest_HashFile>(c => c.FileName = p.FullServerPath);
                }
            }
            catch (Exception ex)
            {
                var msg = $"Error RunImport_IntegrityCheck XREF: {vl.ToStringDetailed()} - {ex}";
                Logger.Info(msg);
            }
        }

        // files which have been hashed, but don't have an associated episode
        var settings = Utils.SettingsProvider.GetSettings();
        var filesWithoutEpisode = RepoFactory.VideoLocal.GetVideosWithoutEpisode();
        foreach (var vl in filesWithoutEpisode)
        {
            if (settings.Import.MaxAutoScanAttemptsPerFile != 0)
            {
                var matchAttempts = RepoFactory.AniDB_FileUpdate.GetByFileSizeAndHash(vl.FileSize, vl.Hash).Count;
                if (matchAttempts > settings.Import.MaxAutoScanAttemptsPerFile)
                    continue;
            }

            commandFactory.CreateAndSave<CommandRequest_ProcessFile>(
                c =>
                {
                    c.VideoLocalID = vl.VideoLocalID;
                    c.ForceAniDB = true;
                }
            );
        }


        // check that all the episode data is populated
        foreach (var vl in RepoFactory.VideoLocal.GetAll().Where(a => !string.IsNullOrEmpty(a.Hash)))
        {
            // if the file is not manually associated, then check for AniDB_File info
            var aniFile = RepoFactory.AniDB_File.GetByHash(vl.Hash);
            foreach (var xref in vl.EpisodeCrossRefs)
            {
                if (xref.CrossRefSource != (int)CrossRefSource.AniDB)
                {
                    continue;
                }

                if (aniFile == null)
                {
                    commandFactory.CreateAndSave<CommandRequest_ProcessFile>(c => c.VideoLocalID = vl.VideoLocalID);
                }
            }

            if (aniFile == null)
            {
                continue;
            }

            // the cross ref is created before the actually episode data is downloaded
            // so lets check for that
            var missingEpisodes = false;
            foreach (var xref in aniFile.EpisodeCrossRefs)
            {
                var ep = RepoFactory.AniDB_Episode.GetByEpisodeID(xref.EpisodeID);
                if (ep == null)
                {
                    missingEpisodes = true;
                }
            }

            if (missingEpisodes)
            {
                // this will then download the anime etc
                commandFactory.CreateAndSave<CommandRequest_ProcessFile>(c => c.VideoLocalID = vl.VideoLocalID);
            }
        }
    }

    public static void RunImport_ScanFolder(int importFolderID, bool skipMyList = false)
    {
        var settings = Utils.SettingsProvider.GetSettings();
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        // get a complete list of files
        var fileList = new List<string>();
        int filesFound = 0, videosFound = 0;
        var i = 0;

        try
        {
            var fldr = RepoFactory.ImportFolder.GetByID(importFolderID);
            if (fldr == null)
            {
                return;
            }

            // first build a list of files that we already know about, as we don't want to process them again


            var filesAll =
                RepoFactory.VideoLocalPlace.GetByImportFolder(fldr.ImportFolderID);
            var dictFilesExisting =
                new Dictionary<string, SVR_VideoLocal_Place>();
            foreach (var vl in filesAll)
            {
                try
                {
                    dictFilesExisting[vl.FullServerPath] = vl;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Error RunImport_ScanFolder XREF: {vl.FullServerPath}");
                }
            }


            Logger.Debug("ImportFolder: {0} || {1}", fldr.ImportFolderName, fldr.ImportFolderLocation);
            Utils.GetFilesForImportFolder(fldr.BaseDirectory, ref fileList);

            // Get Ignored Files and remove them from the scan listing
            var ignoredFiles = RepoFactory.VideoLocal.GetIgnoredVideos().SelectMany(a => a.Places)
                .Select(a => a.FullServerPath).Where(a => !string.IsNullOrEmpty(a)).ToList();
            fileList = fileList.Except(ignoredFiles, StringComparer.InvariantCultureIgnoreCase).ToList();

            // get a list of all files in the share
            foreach (var fileName in fileList)
            {
                i++;

                if (dictFilesExisting.ContainsKey(fileName))
                {
                    if (fldr.IsDropSource == 1)
                    {
                        dictFilesExisting[fileName].RenameAndMoveAsRequired();
                    }
                }

                if (settings.Import.Exclude.Any(s => Regex.IsMatch(fileName, s)))
                {
                    Logger.Trace("Import exclusion, skipping --- {0}", fileName);
                    continue;
                }

                filesFound++;
                Logger.Trace("Processing File {0}/{1} --- {2}", i, fileList.Count, fileName);

                if (!FileHashHelper.IsVideo(fileName))
                {
                    continue;
                }

                videosFound++;

                commandFactory.CreateAndSave<CommandRequest_HashFile>(c =>
                    {
                        c.FileName = fileName;
                        c.SkipMyList = skipMyList;
                    }
                );
            }

            Logger.Debug("Found {0} new files", filesFound);
            Logger.Debug("Found {0} videos", videosFound);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, ex.ToString());
        }
    }


    public static void RunImport_DropFolders()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        // get a complete list of files
        var fileList = new List<string>();
        foreach (var share in RepoFactory.ImportFolder.GetAll())
        {
            if (!share.FolderIsDropSource)
            {
                continue;
            }

            Logger.Debug("ImportFolder: {0} || {1}", share.ImportFolderName, share.ImportFolderLocation);
            Utils.GetFilesForImportFolder(share.BaseDirectory, ref fileList);
        }

        // Get Ignored Files and remove them from the scan listing
        var ignoredFiles = RepoFactory.VideoLocal.GetIgnoredVideos().SelectMany(a => a.Places)
            .Select(a => a.FullServerPath).Where(a => !string.IsNullOrEmpty(a)).ToList();
        fileList = fileList.Except(ignoredFiles, StringComparer.InvariantCultureIgnoreCase).ToList();

        // get a list of all the shares we are looking at
        int filesFound = 0, videosFound = 0;
        var i = 0;

        // get a list of all files in the share
        foreach (var fileName in fileList)
        {
            i++;

            if (settings.Import.Exclude.Any(s => Regex.IsMatch(fileName, s)))
            {
                Logger.Trace("Import exclusion, skipping --- {0}", fileName);
                continue;
            }

            filesFound++;
            Logger.Trace("Processing File {0}/{1} --- {2}", i, fileList.Count, fileName);

            if (!FileHashHelper.IsVideo(fileName))
            {
                continue;
            }

            videosFound++;

            commandFactory.CreateAndSave<CommandRequest_HashFile>(c => c.FileName = fileName);
        }

        Logger.Debug("Found {0} files", filesFound);
        Logger.Debug("Found {0} videos", videosFound);
    }

    public static void RunImport_NewFiles()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        // first build a list of files that we already know about, as we don't want to process them again
        var filesAll = RepoFactory.VideoLocalPlace.GetAll();
        var dictFilesExisting = new Dictionary<string, SVR_VideoLocal_Place>();
        foreach (var vl in filesAll)
        {
            try
            {
                if (vl.FullServerPath == null)
                {
                    Logger.Info("Invalid File Path found. Removing: " + vl.VideoLocal_Place_ID);
                    vl.RemoveRecord();
                    continue;
                }

                dictFilesExisting[vl.FullServerPath] = vl;
            }
            catch (Exception ex)
            {
                var msg = string.Format("Error RunImport_NewFiles XREF: {0} - {1}",
                    (vl.FullServerPath ?? vl.FilePath) ?? vl.VideoLocal_Place_ID.ToString(),
                    ex);
                Logger.Error(msg);
                //throw;
            }
        }


        // Steps for processing a file
        // 1. Check if it is a video file
        // 2. Check if we have a VideoLocal record for that file
        // .........

        // get a complete list of files
        var fileList = new List<string>();
        foreach (var share in RepoFactory.ImportFolder.GetAll())
        {
            Logger.Debug("ImportFolder: {0} || {1}", share.ImportFolderName, share.ImportFolderLocation);
            try
            {
                Utils.GetFilesForImportFolder(share.BaseDirectory, ref fileList);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, ex.ToString());
            }
        }

        // get a list fo files that we haven't processed before
        var fileListNew = new List<string>();
        foreach (var fileName in fileList)
        {
            if (settings.Import.Exclude.Any(s => Regex.IsMatch(fileName, s)))
            {
                Logger.Trace("Import exclusion, skipping --- {0}", fileName);
                continue;
            }

            if (!dictFilesExisting.ContainsKey(fileName))
            {
                fileListNew.Add(fileName);
            }
        }

        // get a list of all the shares we are looking at
        int filesFound = 0, videosFound = 0;
        var i = 0;

        // get a list of all files in the share
        foreach (var fileName in fileListNew)
        {
            i++;
            filesFound++;
            Logger.Trace("Processing File {0}/{1} --- {2}", i, fileList.Count, fileName);

            if (!FileHashHelper.IsVideo(fileName))
            {
                continue;
            }

            videosFound++;

            var tup = VideoLocal_PlaceRepository.GetFromFullPath(fileName);
            ShokoEventHandler.Instance.OnFileDetected(tup.Item1, new FileInfo(fileName));

            commandFactory.CreateAndSave<CommandRequest_HashFile>(c => c.FileName = fileName);
        }

        Logger.Debug("Found {0} files", filesFound);
        Logger.Debug("Found {0} videos", videosFound);
    }

    public static void RunImport_GetImages()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        // AniDB posters
        foreach (var anime in RepoFactory.AniDB_Anime.GetAll())
        {
            if (string.IsNullOrEmpty(anime.PosterPath))
            {
                continue;
            }

            var fileExists = File.Exists(anime.PosterPath);
            if (fileExists)
            {
                continue;
            }

            commandFactory.CreateAndSave<CommandRequest_DownloadAniDBImages>(c => c.AnimeID = anime.AnimeID);
        }

        // TvDB Posters
        if (settings.TvDB.AutoPosters)
        {
            var postersCount = new Dictionary<int, int>();

            // build a dictionary of series and how many images exist
            var allPosters = RepoFactory.TvDB_ImagePoster.GetAll();
            foreach (var tvPoster in allPosters)
            {
                if (string.IsNullOrEmpty(tvPoster.GetFullImagePath()))
                {
                    continue;
                }

                var fileExists = File.Exists(tvPoster.GetFullImagePath());

                if (!fileExists)
                {
                    continue;
                }

                if (postersCount.ContainsKey(tvPoster.SeriesID))
                {
                    postersCount[tvPoster.SeriesID] += 1;
                }
                else
                {
                    postersCount[tvPoster.SeriesID] = 1;
                }
            }

            foreach (var tvPoster in allPosters)
            {
                if (string.IsNullOrEmpty(tvPoster.GetFullImagePath()))
                {
                    continue;
                }

                var fileExists = File.Exists(tvPoster.GetFullImagePath());

                var postersAvailable = 0;
                if (postersCount.ContainsKey(tvPoster.SeriesID))
                {
                    postersAvailable = postersCount[tvPoster.SeriesID];
                }

                if (fileExists || postersAvailable >= settings.TvDB.AutoPostersAmount)
                {
                    continue;
                }

                commandFactory.CreateAndSave<CommandRequest_DownloadImage>(
                    c =>
                    {
                        c.EntityID = tvPoster.TvDB_ImagePosterID;
                        c.EntityType = (int)ImageEntityType.TvDB_Cover;
                    }
                );

                if (postersCount.ContainsKey(tvPoster.SeriesID))
                {
                    postersCount[tvPoster.SeriesID] += 1;
                }
                else
                {
                    postersCount[tvPoster.SeriesID] = 1;
                }
            }
        }

        // TvDB Fanart
        if (settings.TvDB.AutoFanart)
        {
            var fanartCount = new Dictionary<int, int>();
            var allFanart = RepoFactory.TvDB_ImageFanart.GetAll();
            foreach (var tvFanart in allFanart)
            {
                // build a dictionary of series and how many images exist
                if (string.IsNullOrEmpty(tvFanart.GetFullImagePath()))
                {
                    continue;
                }

                var fileExists = File.Exists(tvFanart.GetFullImagePath());

                if (!fileExists)
                {
                    continue;
                }

                if (fanartCount.ContainsKey(tvFanart.SeriesID))
                {
                    fanartCount[tvFanart.SeriesID] += 1;
                }
                else
                {
                    fanartCount[tvFanart.SeriesID] = 1;
                }
            }

            foreach (var tvFanart in allFanart)
            {
                if (string.IsNullOrEmpty(tvFanart.GetFullImagePath()))
                {
                    continue;
                }

                var fileExists = File.Exists(tvFanart.GetFullImagePath());

                var fanartAvailable = 0;
                if (fanartCount.ContainsKey(tvFanart.SeriesID))
                {
                    fanartAvailable = fanartCount[tvFanart.SeriesID];
                }

                if (fileExists || fanartAvailable >= settings.TvDB.AutoFanartAmount)
                {
                    continue;
                }

                commandFactory.CreateAndSave<CommandRequest_DownloadImage>(
                    c =>
                    {
                        c.EntityID = tvFanart.TvDB_ImageFanartID;
                        c.EntityType = (int)ImageEntityType.TvDB_FanArt;
                    }
                );

                if (fanartCount.ContainsKey(tvFanart.SeriesID))
                {
                    fanartCount[tvFanart.SeriesID] += 1;
                }
                else
                {
                    fanartCount[tvFanart.SeriesID] = 1;
                }
            }
        }

        // TvDB Wide Banners
        if (settings.TvDB.AutoWideBanners)
        {
            var fanartCount = new Dictionary<int, int>();

            // build a dictionary of series and how many images exist
            var allBanners = RepoFactory.TvDB_ImageWideBanner.GetAll();
            foreach (var tvBanner in allBanners)
            {
                if (string.IsNullOrEmpty(tvBanner.GetFullImagePath()))
                {
                    continue;
                }

                var fileExists = File.Exists(tvBanner.GetFullImagePath());

                if (!fileExists)
                {
                    continue;
                }

                if (fanartCount.ContainsKey(tvBanner.SeriesID))
                {
                    fanartCount[tvBanner.SeriesID] += 1;
                }
                else
                {
                    fanartCount[tvBanner.SeriesID] = 1;
                }
            }

            foreach (var tvBanner in allBanners)
            {
                if (string.IsNullOrEmpty(tvBanner.GetFullImagePath()))
                {
                    continue;
                }

                var fileExists = File.Exists(tvBanner.GetFullImagePath());

                var bannersAvailable = 0;
                if (fanartCount.ContainsKey(tvBanner.SeriesID))
                {
                    bannersAvailable = fanartCount[tvBanner.SeriesID];
                }

                if (fileExists || bannersAvailable >= settings.TvDB.AutoWideBannersAmount)
                {
                    continue;
                }

                commandFactory.CreateAndSave<CommandRequest_DownloadImage>(
                    c =>
                    {
                        c.EntityID = tvBanner.TvDB_ImageWideBannerID;
                        c.EntityType = (int)ImageEntityType.TvDB_Banner;
                    }
                );

                if (fanartCount.ContainsKey(tvBanner.SeriesID))
                {
                    fanartCount[tvBanner.SeriesID] += 1;
                }
                else
                {
                    fanartCount[tvBanner.SeriesID] = 1;
                }
            }
        }

        // TvDB Episodes

        foreach (var tvEpisode in RepoFactory.TvDB_Episode.GetAll())
        {
            if (string.IsNullOrEmpty(tvEpisode.GetFullImagePath()))
            {
                continue;
            }

            var fileExists = File.Exists(tvEpisode.GetFullImagePath());
            if (fileExists)
            {
                continue;
            }

            commandFactory.CreateAndSave<CommandRequest_DownloadImage>(
                c =>
                {
                    c.EntityID = tvEpisode.TvDB_EpisodeID;
                    c.EntityType = (int)ImageEntityType.TvDB_Episode;
                }
            );
        }

        // MovieDB Posters
        if (settings.MovieDb.AutoPosters)
        {
            var postersCount = new Dictionary<int, int>();

            // build a dictionary of series and how many images exist
            var allPosters = RepoFactory.MovieDB_Poster.GetAll();
            foreach (var moviePoster in allPosters)
            {
                if (string.IsNullOrEmpty(moviePoster.GetFullImagePath()))
                {
                    continue;
                }

                var fileExists = File.Exists(moviePoster.GetFullImagePath());

                if (!fileExists)
                {
                    continue;
                }

                if (postersCount.ContainsKey(moviePoster.MovieId))
                {
                    postersCount[moviePoster.MovieId] += 1;
                }
                else
                {
                    postersCount[moviePoster.MovieId] = 1;
                }
            }

            foreach (var moviePoster in allPosters)
            {
                if (string.IsNullOrEmpty(moviePoster.GetFullImagePath()))
                {
                    continue;
                }

                var fileExists = File.Exists(moviePoster.GetFullImagePath());

                var postersAvailable = 0;
                if (postersCount.ContainsKey(moviePoster.MovieId))
                {
                    postersAvailable = postersCount[moviePoster.MovieId];
                }

                if (fileExists || postersAvailable >= settings.MovieDb.AutoPostersAmount)
                {
                    continue;
                }

                commandFactory.CreateAndSave<CommandRequest_DownloadImage>(
                    c =>
                    {
                        c.EntityID = moviePoster.MovieDB_PosterID;
                        c.EntityType = (int)ImageEntityType.MovieDB_Poster;
                    }
                );

                if (postersCount.ContainsKey(moviePoster.MovieId))
                {
                    postersCount[moviePoster.MovieId] += 1;
                }
                else
                {
                    postersCount[moviePoster.MovieId] = 1;
                }
            }
        }

        // MovieDB Fanart
        if (settings.MovieDb.AutoFanart)
        {
            var fanartCount = new Dictionary<int, int>();

            // build a dictionary of series and how many images exist
            var allFanarts = RepoFactory.MovieDB_Fanart.GetAll();
            foreach (var movieFanart in allFanarts)
            {
                if (string.IsNullOrEmpty(movieFanart.GetFullImagePath()))
                {
                    continue;
                }

                var fileExists = File.Exists(movieFanart.GetFullImagePath());

                if (!fileExists)
                {
                    continue;
                }

                if (fanartCount.ContainsKey(movieFanart.MovieId))
                {
                    fanartCount[movieFanart.MovieId] += 1;
                }
                else
                {
                    fanartCount[movieFanart.MovieId] = 1;
                }
            }

            foreach (var movieFanart in RepoFactory.MovieDB_Fanart.GetAll())
            {
                if (string.IsNullOrEmpty(movieFanart.GetFullImagePath()))
                {
                    continue;
                }

                var fileExists = File.Exists(movieFanart.GetFullImagePath());

                var fanartAvailable = 0;
                if (fanartCount.ContainsKey(movieFanart.MovieId))
                {
                    fanartAvailable = fanartCount[movieFanart.MovieId];
                }

                if (fileExists || fanartAvailable >= settings.MovieDb.AutoFanartAmount)
                {
                    continue;
                }

                commandFactory.CreateAndSave<CommandRequest_DownloadImage>(
                    c =>
                    {
                        c.EntityID = movieFanart.MovieDB_FanartID;
                        c.EntityType = (int)ImageEntityType.MovieDB_FanArt;
                    }
                );

                if (fanartCount.ContainsKey(movieFanart.MovieId))
                {
                    fanartCount[movieFanart.MovieId] += 1;
                }
                else
                {
                    fanartCount[movieFanart.MovieId] = 1;
                }
            }
        }

        // AniDB Characters
        if (settings.AniDb.DownloadCharacters)
        {
            foreach (var chr in RepoFactory.AniDB_Character.GetAll())
            {
                if (string.IsNullOrEmpty(chr.GetPosterPath()))
                {
                    continue;
                }

                var fileExists = File.Exists(chr.GetPosterPath());
                if (fileExists)
                {
                    continue;
                }

                var AnimeID = RepoFactory.AniDB_Anime_Character.GetByCharID(chr.CharID)?.FirstOrDefault()
                    ?.AnimeID ?? 0;
                if (AnimeID == 0)
                {
                    continue;
                }

                commandFactory.CreateAndSave<CommandRequest_DownloadAniDBImages>(c => c.AnimeID = AnimeID);
            }
        }

        // AniDB Creators
        if (settings.AniDb.DownloadCreators)
        {
            foreach (var seiyuu in RepoFactory.AniDB_Seiyuu.GetAll())
            {
                if (string.IsNullOrEmpty(seiyuu.GetPosterPath()))
                {
                    continue;
                }

                var fileExists = File.Exists(seiyuu.GetPosterPath());
                if (fileExists)
                {
                    continue;
                }

                var chr = RepoFactory.AniDB_Character_Seiyuu.GetBySeiyuuID(seiyuu.SeiyuuID).FirstOrDefault();
                if (chr == null)
                {
                    continue;
                }

                var AnimeID = RepoFactory.AniDB_Anime_Character.GetByCharID(chr.CharID)?.FirstOrDefault()
                    ?.AnimeID ?? 0;
                if (AnimeID == 0)
                {
                    continue;
                }

                commandFactory.CreateAndSave<CommandRequest_DownloadAniDBImages>(c => c.AnimeID = AnimeID);
            }
        }
    }

    public static void ValidateAllImages()
    {
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        commandFactory.CreateAndSave<CommandRequest_ValidateAllImages>();
    }

    public static void RunImport_ScanTvDB()
    {
        var tvDBHelper = Utils.ServiceContainer.GetRequiredService<TvDBApiHelper>();

        tvDBHelper.ScanForMatches();
    }

    public static void RunImport_ScanTrakt()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken))
        {
            return;
        }

        var traktHelper = Utils.ServiceContainer.GetRequiredService<TraktTVHelper>();
        traktHelper.ScanForMatches();
    }

    public static void RunImport_ScanMovieDB()
    {
        var movieDBHelper = Utils.ServiceContainer.GetRequiredService<MovieDBHelper>();
        movieDBHelper.ScanForMatches();
    }

    public static void RunImport_UpdateTvDB(bool forced)
    {
        var tvDBHelper = Utils.ServiceContainer.GetRequiredService<TvDBApiHelper>();
        tvDBHelper.UpdateAllInfo(forced);
    }

    public static void RunImport_UpdateAllAniDB()
    {
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();

        foreach (var anime in RepoFactory.AniDB_Anime.GetAll())
        {
            commandFactory.CreateAndSave<CommandRequest_GetAnimeHTTP>(
                c =>
                {
                    c.AnimeID = anime.AnimeID;
                    c.ForceRefresh = true;
                }
            );
        }
    }

    public static void RemoveRecordsWithoutPhysicalFiles(bool removeMyList = true)
    {
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        Logger.Info("Remove Missing Files: Start");
        var seriesToUpdate = new HashSet<SVR_AnimeSeries>();
        using var session = DatabaseFactory.SessionFactory.OpenSession();

        // remove missing files in valid import folders
        var filesAll = RepoFactory.VideoLocalPlace.GetAll()
            .Where(a => a.ImportFolder != null)
            .GroupBy(a => a.ImportFolder)
            .ToDictionary(a => a.Key, a => a.ToList());
        foreach (var folder in filesAll.Keys)
        {
            foreach (var vl in filesAll[folder])
            {
                if (File.Exists(vl.FullServerPath))
                {
                    continue;
                }

                // delete video local record
                Logger.Info("Removing Missing File: {0}", vl.VideoLocalID);
                vl.RemoveRecordWithOpenTransaction(session, seriesToUpdate);
            }
        }

        var videoLocalsAll = RepoFactory.VideoLocal.GetAll().ToList();
        // remove empty videolocals
        BaseRepository.Lock(session, videoLocalsAll, (s, vls) =>
        {
            using var transaction = s.BeginTransaction();
            RepoFactory.VideoLocal.DeleteWithOpenTransaction(s, vls.Where(a => a.IsEmpty()).ToList());
            transaction.Commit();
        });

        // Remove duplicate videolocals
        var locals = videoLocalsAll
            .Where(a => !string.IsNullOrWhiteSpace(a.Hash))
            .GroupBy(a => a.Hash)
            .ToDictionary(g => g.Key, g => g.ToList());
        var toRemove = new List<SVR_VideoLocal>();
        var comparer = new VideoLocalComparer();

        foreach (var hash in locals.Keys)
        {
            var values = locals[hash];
            values.Sort(comparer);
            var to = values.First();
            var froms = values.Except(to).ToList();
            foreach (var places in froms.Select(from => from.Places).Where(places => places != null && places.Count != 0))
            {
                BaseRepository.Lock(session, places, (s, ps) =>
                {
                    using var transaction = s.BeginTransaction();
                    foreach (var place in ps)
                    {
                        place.VideoLocalID = to.VideoLocalID;
                        RepoFactory.VideoLocalPlace.SaveWithOpenTransaction(s, place);
                    }

                    transaction.Commit();
                });
            }

            toRemove.AddRange(froms);
        }

        BaseRepository.Lock(session, toRemove, (s, ps) =>
        {
            using var transaction = s.BeginTransaction();
            foreach (var remove in ps)
            {
                RepoFactory.VideoLocal.DeleteWithOpenTransaction(s, remove);
            }

            transaction.Commit();
        });

        // Remove files in invalid import folders
        foreach (var v in videoLocalsAll)
        {
            var places = v.Places;
            if (v.Places?.Count > 0)
            {
                BaseRepository.Lock(session, places, (s, ps) =>
                {
                    using var transaction = s.BeginTransaction();
                    foreach (var place in ps.Where(place => string.IsNullOrWhiteSpace(place?.FullServerPath)))
                    {
                        Logger.Info("RemoveRecordsWithOrphanedImportFolder : {0}", v.FileName);
                        seriesToUpdate.UnionWith(v.GetAnimeEpisodes().Select(a => a.GetAnimeSeries())
                            .DistinctBy(a => a.AnimeSeriesID));
                        RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(s, place);
                    }

                    transaction.Commit();
                });
            }

            // Remove duplicate places
            places = v.Places;
            if (places?.Count == 1)
            {
                continue;
            }

            if (places?.Count > 0)
            {
                places = places.DistinctBy(a => a.FullServerPath).ToList();
                places = v.Places?.Except(places).ToList();
                foreach (var place in places)
                {
                    BaseRepository.Lock(session, place, (s, p) =>
                    {
                        using var transaction = s.BeginTransaction();
                        RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(s, p);
                        transaction.Commit();
                    });
                }
            }

            if (v.Places?.Count > 0) continue;

            // delete video local record
            Logger.Info("RemoveOrphanedVideoLocal : {0}", v.FileName);
            seriesToUpdate.UnionWith(v.GetAnimeEpisodes().Select(a => a.GetAnimeSeries())
                .DistinctBy(a => a.AnimeSeriesID));

            if (removeMyList)
            {
                if (RepoFactory.AniDB_File.GetByHash(v.Hash) == null)
                {
                    var xrefs = RepoFactory.CrossRef_File_Episode.GetByHash(v.Hash);
                    foreach (var xref in xrefs)
                    {
                        var ep = RepoFactory.AniDB_Episode.GetByEpisodeID(xref.EpisodeID);
                        if (ep == null)
                        {
                            continue;
                        }

                        commandFactory.CreateAndSave<CommandRequest_DeleteFileFromMyList>(c =>
                        {
                            c.AnimeID = xref.AnimeID;
                            c.EpisodeType = ep.GetEpisodeTypeEnum();
                            c.EpisodeNumber = ep.EpisodeNumber;
                        });
                    }
                }
                else
                {
                    commandFactory.CreateAndSave<CommandRequest_DeleteFileFromMyList>(
                        c =>
                        {
                            c.Hash = v.Hash;
                            c.FileSize = v.FileSize;
                        }
                    );
                }
            }

            BaseRepository.Lock(session, v, (s, vl) =>
            {
                using var transaction = s.BeginTransaction();
                RepoFactory.VideoLocal.DeleteWithOpenTransaction(s, vl);
                transaction.Commit();
            });
        }

        // Clean up failed imports
        var list = RepoFactory.VideoLocal.GetAll()
            .SelectMany(a => RepoFactory.CrossRef_File_Episode.GetByHash(a.Hash))
            .Where(a => RepoFactory.AniDB_Anime.GetByAnimeID(a.AnimeID) == null ||
                        a.GetEpisode() == null).ToArray();
        BaseRepository.Lock(session, s =>
        {
            using var transaction = s.BeginTransaction();
            foreach (var xref in list)
            {
                // We don't need to update anything since they don't exist
                RepoFactory.CrossRef_File_Episode.DeleteWithOpenTransaction(s, xref);
            }

            transaction.Commit();
        });
        
        // clean up orphaned video local places
        var placesToRemove = RepoFactory.VideoLocalPlace.GetAll().Where(a => a.VideoLocal == null).ToList();
        BaseRepository.Lock(session, s =>
        {
            using var transaction = s.BeginTransaction();
            foreach (var place in placesToRemove)
            {
                // We don't need to update anything since they don't exist
                RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(s, place);
            }

            transaction.Commit();
        });

        // update everything we modified
        foreach (var ser in seriesToUpdate)
        {
            ser.QueueUpdateStats();
        }

        Logger.Info("Remove Missing Files: Finished");
    }

    public static string DeleteImportFolder(int importFolderID, bool removeFromMyList = true)
    {
        try
        {
            var affectedSeries = new HashSet<SVR_AnimeSeries>();
            var vids = RepoFactory.VideoLocalPlace.GetByImportFolder(importFolderID);
            Logger.Info($"Deleting {vids.Count} video local records");
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            vids.ForEach(vid => vid.RemoveRecordWithOpenTransaction(session, affectedSeries, removeFromMyList, false));

            // delete any duplicate file records which reference this folder
            RepoFactory.DuplicateFile.Delete(RepoFactory.DuplicateFile.GetByImportFolder1(importFolderID));
            RepoFactory.DuplicateFile.Delete(RepoFactory.DuplicateFile.GetByImportFolder2(importFolderID));

            // delete the import folder
            RepoFactory.ImportFolder.Delete(importFolderID);

            foreach (var ser in affectedSeries)
                ser.QueueUpdateStats();

            return string.Empty;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, ex.ToString());
            return ex.Message;
        }
    }

    public static void UpdateAllStats()
    {
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        foreach (var ser in RepoFactory.AnimeSeries.GetAll())
        {
            ser.QueueUpdateStats();
        }

        commandFactory.CreateAndSave<CommandRequest_RefreshGroupFilter>(c => c.GroupFilterID = 0);
    }


    public static int UpdateAniDBFileData(bool missingInfo, bool outOfDate, bool dryRun)
    {
        Logger.Info("Updating Missing AniDB_File Info");
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        var vidsToUpdate = new HashSet<int>();
        var groupsToUpdate = new HashSet<int>();
        if (outOfDate)
        {
            var files = RepoFactory.VideoLocal.GetByInternalVersion(1);

            foreach (var file in files)
            {
                vidsToUpdate.Add(file.VideoLocalID);
            }
        }

        if (missingInfo)
        {
            var anidbReleaseGroupIDs = RepoFactory.AniDB_ReleaseGroup.GetAll()
                .Select(group => group.GroupID)
                .ToHashSet();
            var missingGroups = RepoFactory.AniDB_File.GetAll().Select(a => a.GroupID).Where(a => a != 0 && !anidbReleaseGroupIDs.Contains(a)).ToList();
            groupsToUpdate.UnionWith(missingGroups);

            var missingFiles = RepoFactory.AniDB_File.GetAll()
                .Where(a => a.GroupID == 0)
                .Select(a => RepoFactory.VideoLocal.GetByHash(a.Hash))
                .Where(f => f != null)
                .Select(a => a.VideoLocalID)
                .ToList();
            vidsToUpdate.UnionWith(missingFiles);
        }

        if (!dryRun)
        {
            Logger.Info("Queuing {0} GetFile commands", vidsToUpdate.Count);
            vidsToUpdate.ForEach(id => commandFactory.CreateAndSave<CommandRequest_GetFile>(
                c =>
                {
                    c.VideoLocalID = id;
                    c.ForceAniDB = true;
                }
            ));

            Logger.Info("Queuing {0} GetReleaseGroup commands", groupsToUpdate.Count);
            groupsToUpdate.ForEach(a => commandFactory.CreateAndSave<CommandRequest_GetReleaseGroup>(c => c.GroupID = a));
        }

        return vidsToUpdate.Count;
    }

    public static void CheckForTvDBUpdates(bool forceRefresh)
    {
        var settings = Utils.SettingsProvider.GetSettings();
        if (settings.TvDB.UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh)
        {
            return;
        }

        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        var tvDBHelper = Utils.ServiceContainer.GetRequiredService<TvDBApiHelper>();
        var freqHours = Utils.GetScheduledHours(settings.TvDB.UpdateFrequency);

        // update tvdb info every 12 hours

        var sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TvDBInfo);
        if (sched != null)
        {
            // if we have run this in the last 12 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - sched.LastUpdate;
            if (tsLastRun.TotalHours < freqHours)
            {
                if (!forceRefresh)
                {
                    return;
                }
            }
        }

        var tvDBIDs = new List<int>();
        var tvDBOnline = false;
        var serverTime = tvDBHelper.IncrementalTvDBUpdate(ref tvDBIDs, ref tvDBOnline);

        if (tvDBOnline)
        {
            foreach (var tvid in tvDBIDs)
            {
                // download and update series info, episode info and episode images
                // will also download fanart, posters and wide banners
                commandFactory.CreateAndSave<CommandRequest_TvDBUpdateSeries>(
                    c =>
                    {
                        c.TvDBSeriesID = tvid;
                        c.ForceRefresh = true;
                    }
                );
            }
        }

        if (sched == null)
        {
            sched = new ScheduledUpdate { UpdateType = (int)ScheduledUpdateType.TvDBInfo };
        }

        sched.LastUpdate = DateTime.Now;
        sched.UpdateDetails = serverTime;
        RepoFactory.ScheduledUpdate.Save(sched);

        tvDBHelper.ScanForMatches();
    }

    public static void CheckForCalendarUpdate(bool forceRefresh)
    {
        var settings = Utils.SettingsProvider.GetSettings();
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        if (settings.AniDb.Calendar_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh)
        {
            return;
        }

        var freqHours = Utils.GetScheduledHours(settings.AniDb.Calendar_UpdateFrequency);

        // update the calendar every 12 hours
        // we will always assume that an anime was downloaded via http first


        var sched =
            RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBCalendar);
        if (sched != null)
        {
            // if we have run this in the last 12 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - sched.LastUpdate;
            if (tsLastRun.TotalHours < freqHours)
            {
                if (!forceRefresh)
                {
                    return;
                }
            }
        }

        commandFactory.CreateAndSave<CommandRequest_GetCalendar>(c => c.ForceRefresh = forceRefresh);
    }

    public static void CheckForAnimeUpdate(bool forceRefresh)
    {
        var settings = Utils.SettingsProvider.GetSettings();
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        if (settings.AniDb.Anime_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh)
        {
            return;
        }

        var freqHours = Utils.GetScheduledHours(settings.AniDb.Anime_UpdateFrequency);

        // check for any updated anime info every 12 hours

        var sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBUpdates);
        if (sched != null)
        {
            // if we have run this in the last 12 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - sched.LastUpdate;
            if (tsLastRun.TotalHours < freqHours)
            {
                if (!forceRefresh)
                {
                    return;
                }
            }
        }

        commandFactory.CreateAndSave<CommandRequest_GetUpdated>(c => c.ForceRefresh = true);
    }

    public static void CheckForMyListStatsUpdate(bool forceRefresh)
    {
        // Obsolete. Noop
    }

    public static void CheckForMyListSyncUpdate(bool forceRefresh)
    {
        var settings = Utils.SettingsProvider.GetSettings();
        if (settings.AniDb.MyList_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh)
        {
            return;
        }

        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        var freqHours = Utils.GetScheduledHours(settings.AniDb.MyList_UpdateFrequency);

        // update the calendar every 24 hours

        var sched =
            RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBMyListSync);
        if (sched != null)
        {
            // if we have run this in the last 24 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - sched.LastUpdate;
            Logger.Trace("Last AniDB MyList Sync: {0} minutes ago", tsLastRun.TotalMinutes);
            if (tsLastRun.TotalHours < freqHours)
            {
                if (!forceRefresh)
                {
                    return;
                }
            }
        }

        commandFactory.CreateAndSave<CommandRequest_SyncMyList>(c => c.ForceRefresh = forceRefresh);
    }

    public static void CheckForTraktSyncUpdate(bool forceRefresh)
    {
        var settings = Utils.SettingsProvider.GetSettings();
        if (!settings.TraktTv.Enabled)
        {
            return;
        }

        if (settings.TraktTv.SyncFrequency == ScheduledUpdateFrequency.Never && !forceRefresh)
        {
            return;
        }

        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        var freqHours = Utils.GetScheduledHours(settings.TraktTv.SyncFrequency);

        // update the calendar every xxx hours

        var sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TraktSync);
        if (sched != null)
        {
            // if we have run this in the last xxx hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - sched.LastUpdate;
            Logger.Trace("Last Trakt Sync: {0} minutes ago", tsLastRun.TotalMinutes);
            if (tsLastRun.TotalHours < freqHours)
            {
                if (!forceRefresh)
                {
                    return;
                }
            }
        }

        if (settings.TraktTv.Enabled && !string.IsNullOrEmpty(settings.TraktTv.AuthToken))
        {
            commandFactory.CreateAndSave<CommandRequest_TraktSyncCollection>();
        }
    }

    public static void CheckForTraktAllSeriesUpdate(bool forceRefresh)
    {
        var settings = Utils.SettingsProvider.GetSettings();
        if (!settings.TraktTv.Enabled)
        {
            return;
        }

        if (settings.TraktTv.UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh)
        {
            return;
        }

        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        var freqHours = Utils.GetScheduledHours(settings.TraktTv.UpdateFrequency);

        // update the calendar every xxx hours
        var sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TraktUpdate);
        if (sched != null)
        {
            // if we have run this in the last xxx hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - sched.LastUpdate;
            Logger.Trace("Last Trakt Update: {0} minutes ago", tsLastRun.TotalMinutes);
            if (tsLastRun.TotalHours < freqHours)
            {
                if (!forceRefresh)
                {
                    return;
                }
            }
        }

        commandFactory.CreateAndSave<CommandRequest_TraktUpdateAllSeries>();
    }

    public static void CheckForTraktTokenUpdate(bool forceRefresh)
    {
        try
        {
            var settings = Utils.SettingsProvider.GetSettings();
            if (!settings.TraktTv.Enabled)
            {
                return;
            }

            var traktHelper = Utils.ServiceContainer.GetRequiredService<TraktTVHelper>();
            // by updating the Trakt token regularly, the user won't need to authorize again
            var freqHours = 24; // we need to update this daily

            var sched =
                RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TraktToken);
            if (sched != null)
            {
                // if we have run this in the last xxx hours and are not forcing it, then exit
                var tsLastRun = DateTime.Now - sched.LastUpdate;
                Logger.Trace("Last Trakt Token Update: {0} minutes ago", tsLastRun.TotalMinutes);
                if (tsLastRun.TotalHours < freqHours)
                {
                    if (!forceRefresh)
                    {
                        return;
                    }
                }
            }

            traktHelper.RefreshAuthToken();
            if (sched == null)
            {
                sched = new ScheduledUpdate
                {
                    UpdateType = (int)ScheduledUpdateType.TraktToken, UpdateDetails = string.Empty
                };
            }

            sched.LastUpdate = DateTime.Now;
            RepoFactory.ScheduledUpdate.Save(sched);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error in CheckForTraktTokenUpdate: " + ex);
        }
    }

    public static void CheckForAniDBFileUpdate(bool forceRefresh)
    {
        var settings = Utils.SettingsProvider.GetSettings();
        if (settings.AniDb.File_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh)
        {
            return;
        }

        var freqHours = Utils.GetScheduledHours(settings.AniDb.File_UpdateFrequency);
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();

        // check for any updated anime info every 12 hours

        var sched =
            RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBFileUpdates);
        if (sched != null)
        {
            // if we have run this in the last 12 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - sched.LastUpdate;
            if (tsLastRun.TotalHours < freqHours && !forceRefresh) return;
        }

        // files which have been hashed, but don't have an associated episode
        var filesWithoutEpisode = RepoFactory.VideoLocal.GetVideosWithoutEpisode();
        foreach (var vl in filesWithoutEpisode)
        {
            if (settings.Import.MaxAutoScanAttemptsPerFile != 0)
            {
                var matchAttempts = RepoFactory.AniDB_FileUpdate.GetByFileSizeAndHash(vl.FileSize, vl.Hash).Count;
                if (matchAttempts > settings.Import.MaxAutoScanAttemptsPerFile)
                    continue;
            }

            commandFactory.CreateAndSave<CommandRequest_ProcessFile>(
                c =>
                {
                    c.VideoLocalID = vl.VideoLocalID;
                    c.ForceAniDB = true;
                }
            );
        }

        // now check for any files which have been manually linked and are less than 30 days old


        if (sched == null)
        {
            sched = new ScheduledUpdate
            {
                UpdateType = (int)ScheduledUpdateType.AniDBFileUpdates, UpdateDetails = string.Empty
            };
        }

        sched.LastUpdate = DateTime.Now;
        RepoFactory.ScheduledUpdate.Save(sched);
    }

    public static void CheckForPreviouslyIgnored()
    {
        try
        {
            var filesAll = RepoFactory.VideoLocal.GetAll();
            IReadOnlyList<SVR_VideoLocal> filesIgnored = RepoFactory.VideoLocal.GetIgnoredVideos();

            foreach (var vl in filesAll)
            {
                if (!vl.IsIgnored)
                {
                    // Check if we have this file marked as previously ignored, matches only if it has the same hash
                    var resultVideoLocalsIgnored =
                        filesIgnored.Where(s => s.Hash == vl.Hash).ToList();

                    if (resultVideoLocalsIgnored.Any())
                    {
                        vl.IsIgnored = true;
                        RepoFactory.VideoLocal.Save(vl, false);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, string.Format("Error in CheckForPreviouslyIgnored: {0}", ex));
        }
    }
}
