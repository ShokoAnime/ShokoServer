using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentNHibernate.Utils;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.FileHelper;
using Shoko.Server.Models;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Scheduling.Jobs.TMDB;
using Shoko.Server.Scheduling.Jobs.Trakt;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Utils = Shoko.Server.Utilities.Utils;

namespace Shoko.Server.Services;

public class ActionService
{
    private readonly ILogger<ActionService> _logger;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ISettingsProvider _settingsProvider;
    private readonly VideoLocal_PlaceService _placeService;
    private readonly TmdbMetadataService _tmdbService;
    private readonly TraktTVHelper _traktHelper;
    private readonly ImportFolderRepository _importFolders;
    private readonly DatabaseFactory _databaseFactory;

    public ActionService(
        ILogger<ActionService> logger,
        ISchedulerFactory schedulerFactory,
        ISettingsProvider settingsProvider,
        VideoLocal_PlaceService placeService,
        TraktTVHelper traktHelper,
        TmdbMetadataService tmdbService,
        ImportFolderRepository importFolders,
        DatabaseFactory databaseFactory
    )
    {
        _logger = logger;
        _schedulerFactory = schedulerFactory;
        _settingsProvider = settingsProvider;
        _placeService = placeService;
        _traktHelper = traktHelper;
        _tmdbService = tmdbService;
        _importFolders = importFolders;
        _databaseFactory = databaseFactory;
    }

    public async Task RunImport_IntegrityCheck()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        // files which have not been hashed yet
        // or files which do not have a VideoInfo record
        var filesToHash = RepoFactory.VideoLocal.GetVideosWithoutHash();
        var dictFilesToHash = new Dictionary<int, SVR_VideoLocal>();
        foreach (var vl in filesToHash)
        {
            dictFilesToHash[vl.VideoLocalID] = vl;
            var p = vl.FirstResolvedPlace;
            if (p == null) continue;

            await scheduler.StartJob<HashFileJob>(c => c.FilePath = p.FullServerPath);
        }

        foreach (var vl in filesToHash)
        {
            // don't use if it is in the previous list
            if (dictFilesToHash.ContainsKey(vl.VideoLocalID)) continue;

            try
            {
                var p = vl.FirstResolvedPlace;
                if (p == null) continue;

                await scheduler.StartJob<HashFileJob>(c => c.FilePath = p.FullServerPath);
            }
            catch (Exception ex)
            {
                _logger.LogInformation("Error RunImport_IntegrityCheck XREF: {Detailed} - {Ex}", vl.ToStringDetailed(), ex.ToString());
            }
        }

        // files which have been hashed, but don't have an associated episode
        var settings = _settingsProvider.GetSettings();
        var filesWithoutEpisode = RepoFactory.VideoLocal.GetVideosWithoutEpisode();
        foreach (var vl in filesWithoutEpisode)
        {
            if (settings.Import.MaxAutoScanAttemptsPerFile != 0)
            {
                var matchAttempts = RepoFactory.AniDB_FileUpdate.GetByFileSizeAndHash(vl.FileSize, vl.Hash).Count;
                if (matchAttempts > settings.Import.MaxAutoScanAttemptsPerFile)
                    continue;
            }

            await scheduler.StartJob<ProcessFileJob>(
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
            // queue scan for files that are automatically linked but missing AniDB_File data
            var aniFile = RepoFactory.AniDB_File.GetByHash(vl.Hash);
            if (aniFile == null && vl.EpisodeCrossRefs.Any(a => a.CrossRefSource == (int)CrossRefSource.AniDB))
                await scheduler.StartJob<ProcessFileJob>(c => c.VideoLocalID = vl.VideoLocalID);

            if (aniFile == null) continue;

            // the cross ref is created before the actually episode data is downloaded
            // so lets check for that
            var missingEpisodes = aniFile.EpisodeCrossRefs.Any(a => RepoFactory.AniDB_Episode.GetByEpisodeID(a.EpisodeID) == null);

            // this will then download the anime etc
            if (missingEpisodes) await scheduler.StartJob<ProcessFileJob>(c => c.VideoLocalID = vl.VideoLocalID);
        }
    }

    public async Task RunImport_ScanFolder(int importFolderID, bool skipMyList = false)
    {
        var settings = _settingsProvider.GetSettings();
        var scheduler = await _schedulerFactory.GetScheduler();

        // get a complete list of files
        var fileList = new List<string>();
        var filesFound = 0;
        var videosFound = 0;
        var i = 0;

        try
        {
            var folder = RepoFactory.ImportFolder.GetByID(importFolderID);
            if (folder == null) return;

            // first build a list of files that we already know about, as we don't want to process them again
            var filesAll = RepoFactory.VideoLocalPlace.GetByImportFolder(folder.ImportFolderID);
            var dictFilesExisting = new Dictionary<string, SVR_VideoLocal_Place>();
            foreach (var vl in filesAll.Where(a => a.FullServerPath != null))
            {
                dictFilesExisting[vl.FullServerPath] = vl;
            }

            var baseDirectory = folder.BaseDirectory;
            var comparer = Utils.GetComparerFor(baseDirectory.FullName);
            Utils.GetFilesForImportFolder(baseDirectory, ref fileList);

            // Get Ignored Files and remove them from the scan listing
            var ignoredFiles = RepoFactory.VideoLocal.GetIgnoredVideos().SelectMany(a => a.Places)
                .Select(a => a.FullServerPath).Where(a => !string.IsNullOrEmpty(a)).ToList();
            fileList = fileList.Except(ignoredFiles, comparer).ToList();

            // get a list of all files in the share
            foreach (var fileName in fileList)
            {
                i++;

                if (dictFilesExisting.TryGetValue(fileName, out var value) && folder.IsDropSource == 1)
                    await _placeService.AutoRelocateFile(value);

                if (settings.Import.Exclude.Any(s => Regex.IsMatch(fileName, s)))
                {
                    _logger.LogTrace("Import exclusion, skipping --- {Filename}", fileName);
                    continue;
                }

                filesFound++;
                _logger.LogTrace("Processing File {Count}/{Total} --- {Filename}", i, fileList.Count, fileName);

                if (!FileHashHelper.IsVideo(fileName)) continue;

                videosFound++;

                await scheduler.StartJob<DiscoverFileJob>(a =>
                {
                    a.FilePath = fileName;
                    a.SkipMyList = skipMyList;
                });
            }

            _logger.LogDebug("Found {Count} new files", filesFound);
            _logger.LogDebug("Found {Count} videos", videosFound);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ex}", ex.ToString());
        }
    }

    public async Task RunImport_DropFolders()
    {
        var settings = _settingsProvider.GetSettings();
        var scheduler = await _schedulerFactory.GetScheduler();
        // Get Ignored Files and remove them from the scan listing
        var ignoredFiles = RepoFactory.VideoLocal.GetIgnoredVideos()
            .SelectMany(a => a.Places)
            .Select(a => a.FullServerPath)
            .Where(a => !string.IsNullOrEmpty(a))
            .ToList();

        // get a complete list of files
        var fileList = new List<string>();
        foreach (var folder in RepoFactory.ImportFolder.GetAll())
        {
            if (!folder.FolderIsDropSource) continue;

            var fileListForFolder = new List<string>();
            var baseDirectory = folder.BaseDirectory;
            var comparer = Utils.GetComparerFor(baseDirectory.FullName);
            Utils.GetFilesForImportFolder(baseDirectory, ref fileListForFolder);
            fileListForFolder = fileListForFolder.Except(ignoredFiles, comparer).ToList();
            fileList.AddRange(fileListForFolder);
        }

        // get a list of all the shares we are looking at
        int filesFound = 0, videosFound = 0;
        var i = 0;

        // get a list of all files in the share
        foreach (var fileName in fileList)
        {
            i++;

            if (settings.Import.Exclude.Any(s => Regex.IsMatch(fileName, s)))
            {
                _logger.LogTrace("Import exclusion, skipping --- {Name}", fileName);
                continue;
            }

            filesFound++;
            _logger.LogTrace("Processing File {Count}/{Total} --- {Name}", i, fileList.Count, fileName);

            if (!FileHashHelper.IsVideo(fileName)) continue;
            videosFound++;

            await scheduler.StartJob<DiscoverFileJob>(a => a.FilePath = fileName);
        }

        _logger.LogDebug("Found {Count} files", filesFound);
        _logger.LogDebug("Found {Count} videos", videosFound);
    }

    public async Task RunImport_NewFiles()
    {
        var settings = _settingsProvider.GetSettings();
        var scheduler = await _schedulerFactory.GetScheduler();
        // first build a list of files that we already know about, as we don't want to process them again
        var filesAll = RepoFactory.VideoLocalPlace.GetAll();
        var dictFilesExisting = new Dictionary<string, SVR_VideoLocal_Place>();
        foreach (var vl in filesAll)
        {
            try
            {
                if (vl.FullServerPath == null)
                {
                    _logger.LogInformation("Invalid File Path found. Removing: {ID}", vl.VideoLocal_Place_ID);
                    await _placeService.RemoveRecord(vl);
                    continue;
                }

                dictFilesExisting[vl.FullServerPath] = vl;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error RunImport_NewFiles XREF: {Path} - {Ex}", (vl.FullServerPath ?? vl.FilePath) ?? vl.VideoLocal_Place_ID.ToString(),
                    ex.ToString());
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
            try
            {
                Utils.GetFilesForImportFolder(share.BaseDirectory, ref fileList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ex}", ex.ToString());
            }
        }

        // get a list fo files that we haven't processed before
        var fileListNew = new List<string>();
        foreach (var fileName in fileList)
        {
            if (settings.Import.Exclude.Any(s => Regex.IsMatch(fileName, s)))
            {
                _logger.LogTrace("Import exclusion, skipping --- {Name}", fileName);
                continue;
            }

            if (!dictFilesExisting.ContainsKey(fileName)) fileListNew.Add(fileName);
        }

        // get a list of all the shares we are looking at
        var filesFound = 0;
        var videosFound = 0;
        var i = 0;

        // get a list of all files in the share
        foreach (var fileName in fileListNew)
        {
            i++;
            filesFound++;
            _logger.LogTrace("Processing File {Count}/{Total} --- {Name}", i, fileList.Count, fileName);

            if (!FileHashHelper.IsVideo(fileName)) continue;
            videosFound++;

            var (folder, relativePath) = _importFolders.GetFromFullPath(fileName);
            ShokoEventHandler.Instance.OnFileDetected(folder, new FileInfo(fileName));

            await scheduler.StartJob<DiscoverFileJob>(a => a.FilePath = fileName);
        }

        _logger.LogDebug("Found {Count} files", filesFound);
        _logger.LogDebug("Found {Count} videos", videosFound);
    }

    public async Task RunImport_GetImages()
    {
        var settings = _settingsProvider.GetSettings();
        var scheduler = await _schedulerFactory.GetScheduler();
        // AniDB images
        foreach (var anime in RepoFactory.AniDB_Anime.GetAll())
        {
            var updateImages = false;
            // poster
            if (!string.IsNullOrEmpty(anime.PosterPath)) updateImages |= !File.Exists(anime.PosterPath);

            var seriesExists = RepoFactory.AnimeSeries.GetByAnimeID(anime.AnimeID) != null;
            if (seriesExists)
            {
                // characters
                updateImages |= ShouldUpdateAniDBCharacterImages(settings, anime);

                // creators
                updateImages |= ShouldUpdateAniDBCreatorImages(settings, anime);
            }

            if (!updateImages) continue;
            await scheduler.StartJob<GetAniDBImagesJob>(c =>
            {
                c.AnimeID = anime.AnimeID;
                c.OnlyPosters = !seriesExists;
            });
        }

        // TMDB Images
        if (settings.TMDB.AutoDownloadPosters)
            await RunImport_DownloadTmdbImagesForType(_schedulerFactory, ImageEntityType.Poster, settings.TMDB.MaxAutoPosters);
        if (settings.TMDB.AutoDownloadLogos)
            await RunImport_DownloadTmdbImagesForType(_schedulerFactory, ImageEntityType.Logo, settings.TMDB.MaxAutoLogos);
        if (settings.TMDB.AutoDownloadBackdrops)
            await RunImport_DownloadTmdbImagesForType(_schedulerFactory, ImageEntityType.Backdrop, settings.TMDB.MaxAutoBackdrops);
        if (settings.TMDB.AutoDownloadStaffImages)
            await RunImport_DownloadTmdbImagesForType(_schedulerFactory, ImageEntityType.Person, settings.TMDB.MaxAutoStaffImages);
        if (settings.TMDB.AutoDownloadThumbnails)
            await RunImport_DownloadTmdbImagesForType(_schedulerFactory, ImageEntityType.Thumbnail, settings.TMDB.MaxAutoThumbnails);
    }

    private static async Task RunImport_DownloadTmdbImagesForType(ISchedulerFactory schedulerFactory, ImageEntityType type, int maxCount)
    {
        // Build a few dictionaries to check how many images exist for each type.
        var countsForMovies = new Dictionary<int, int>();
        var countForEpisodes = new Dictionary<int, int>();
        var countForSeasons = new Dictionary<int, int>();
        var countForShows = new Dictionary<int, int>();
        var countForCollections = new Dictionary<int, int>();
        var countForNetworks = new Dictionary<int, int>();
        var countForCompanies = new Dictionary<int, int>();
        var countForPersons = new Dictionary<int, int>();
        var allImages = RepoFactory.TMDB_Image.GetByType(type);
        foreach (var image in allImages)
        {
            var path = image.LocalPath;
            if (string.IsNullOrEmpty(path))
                continue;

            if (!File.Exists(path))
                continue;

            if (image.TmdbMovieID.HasValue)
                if (countsForMovies.ContainsKey(image.TmdbMovieID.Value))
                    countsForMovies[image.TmdbMovieID.Value] += 1;
                else
                    countsForMovies[image.TmdbMovieID.Value] = 1;
            if (image.TmdbEpisodeID.HasValue)
                if (countForEpisodes.ContainsKey(image.TmdbEpisodeID.Value))
                    countForEpisodes[image.TmdbEpisodeID.Value] += 1;
                else
                    countForEpisodes[image.TmdbEpisodeID.Value] = 1;
            if (image.TmdbSeasonID.HasValue)
                if (countForSeasons.ContainsKey(image.TmdbSeasonID.Value))
                    countForSeasons[image.TmdbSeasonID.Value] += 1;
                else
                    countForSeasons[image.TmdbSeasonID.Value] = 1;
            if (image.TmdbShowID.HasValue)
                if (countForShows.ContainsKey(image.TmdbShowID.Value))
                    countForShows[image.TmdbShowID.Value] += 1;
                else
                    countForShows[image.TmdbShowID.Value] = 1;
            if (image.TmdbCollectionID.HasValue)
                if (countForCollections.ContainsKey(image.TmdbCollectionID.Value))
                    countForCollections[image.TmdbCollectionID.Value] += 1;
                else
                    countForCollections[image.TmdbCollectionID.Value] = 1;
            if (image.TmdbNetworkID.HasValue)
                if (countForNetworks.ContainsKey(image.TmdbNetworkID.Value))
                    countForNetworks[image.TmdbNetworkID.Value] += 1;
                else
                    countForNetworks[image.TmdbNetworkID.Value] = 1;
            if (image.TmdbCompanyID.HasValue)
                if (countForCompanies.ContainsKey(image.TmdbCompanyID.Value))
                    countForCompanies[image.TmdbCompanyID.Value] += 1;
                else
                    countForCompanies[image.TmdbCompanyID.Value] = 1;
            if (image.TmdbPersonID.HasValue)
                if (countForPersons.ContainsKey(image.TmdbPersonID.Value))
                    countForPersons[image.TmdbPersonID.Value] += 1;
                else
                    countForPersons[image.TmdbPersonID.Value] = 1;
        }

        var scheduler = await schedulerFactory.GetScheduler();
        foreach (var image in allImages)
        {
            var path = image.LocalPath;
            if (string.IsNullOrEmpty(path) || File.Exists(path))
                continue;

            // Check if we should download the image or not.
            var limitEnabled = maxCount > 0;
            var shouldDownload = !limitEnabled;
            if (limitEnabled)
            {
                if (countsForMovies.TryGetValue(image.TmdbMovieID ?? 0, out var count) && count < maxCount)
                    shouldDownload = true;
                if (countForEpisodes.TryGetValue(image.TmdbEpisodeID ?? 0, out count) && count < maxCount)
                    shouldDownload = true;
                if (countForSeasons.TryGetValue(image.TmdbSeasonID ?? 0, out count) && count < maxCount)
                    shouldDownload = true;
                if (countForShows.TryGetValue(image.TmdbShowID ?? 0, out count) && count < maxCount)
                    shouldDownload = true;
                if (countForCollections.TryGetValue(image.TmdbCollectionID ?? 0, out count) && count < maxCount)
                    shouldDownload = true;
                if (countForNetworks.TryGetValue(image.TmdbNetworkID ?? 0, out count) && count < maxCount)
                    shouldDownload = true;
                if (countForCompanies.TryGetValue(image.TmdbCompanyID ?? 0, out count) && count < maxCount)
                    shouldDownload = true;
                if (countForPersons.TryGetValue(image.TmdbPersonID ?? 0, out count) && count < maxCount)
                    shouldDownload = true;
            }

            if (shouldDownload)
            {
                await scheduler.StartJob<DownloadTmdbImageJob>(c =>
                {
                    c.ImageID = image.TMDB_ImageID;
                    c.ImageType = image.ImageType;
                });

                if (image.TmdbMovieID.HasValue)
                    if (countsForMovies.ContainsKey(image.TmdbMovieID.Value))
                        countsForMovies[image.TmdbMovieID.Value] += 1;
                    else
                        countsForMovies[image.TmdbMovieID.Value] = 1;
                if (image.TmdbSeasonID.HasValue)
                    if (countForSeasons.ContainsKey(image.TmdbSeasonID.Value))
                        countForSeasons[image.TmdbSeasonID.Value] += 1;
                    else
                        countForSeasons[image.TmdbSeasonID.Value] = 1;
                if (image.TmdbShowID.HasValue)
                    if (countForShows.ContainsKey(image.TmdbShowID.Value))
                        countForShows[image.TmdbShowID.Value] += 1;
                    else
                        countForShows[image.TmdbShowID.Value] = 1;
                if (image.TmdbCollectionID.HasValue)
                    if (countForCollections.ContainsKey(image.TmdbCollectionID.Value))
                        countForCollections[image.TmdbCollectionID.Value] += 1;
                    else
                        countForCollections[image.TmdbCollectionID.Value] = 1;
            }
        }
    }

    private static bool ShouldUpdateAniDBCreatorImages(IServerSettings settings, SVR_AniDB_Anime anime)
    {
        if (!settings.AniDb.DownloadCreators) return false;

        foreach (var seiyuu in RepoFactory.AniDB_Character.GetCharactersForAnime(anime.AnimeID)
                    .SelectMany(a => RepoFactory.AniDB_Character_Creator.GetByCharacterID(a.CharID))
                    .Select(a => RepoFactory.AniDB_Creator.GetByCreatorID(a.CreatorID)).WhereNotNull())
        {
            if (string.IsNullOrEmpty(seiyuu.ImagePath)) continue;
            if (!File.Exists(seiyuu.GetFullImagePath())) return true;
        }

        foreach (var seiyuu in RepoFactory.AniDB_Anime_Staff.GetByAnimeID(anime.AnimeID)
                    .Select(a => RepoFactory.AniDB_Creator.GetByCreatorID(a.CreatorID)).WhereNotNull())
        {
            if (string.IsNullOrEmpty(seiyuu.ImagePath)) continue;
            if (!File.Exists(seiyuu.GetFullImagePath())) return true;
        }

        return false;
    }

    private static bool ShouldUpdateAniDBCharacterImages(IServerSettings settings, SVR_AniDB_Anime anime)
    {
        if (!settings.AniDb.DownloadCharacters) return false;

        foreach (var chr in RepoFactory.AniDB_Character.GetCharactersForAnime(anime.AnimeID))
        {
            if (string.IsNullOrEmpty(chr.PicName)) continue;
            if (!File.Exists(chr.GetFullImagePath())) return true;
        }

        return false;
    }

    public void RunImport_ScanTrakt()
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken)) return;
        _traktHelper.ScanForMatches();
    }

    public async Task RunImport_ScanTMDB()
    {
        await _tmdbService.ScanForMatches();
    }

    public async Task RunImport_UpdateAllAniDB()
    {
        var settings = _settingsProvider.GetSettings();
        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var anime in RepoFactory.AniDB_Anime.GetAll())
        {
            await scheduler.StartJob<GetAniDBAnimeJob>(c =>
            {
                c.AnimeID = anime.AnimeID;
                c.ForceRefresh = true;
                c.CacheOnly = false;
                c.DownloadRelations = settings.AniDb.DownloadRelatedAnime;
            });
        }
    }

    public async Task RemoveRecordsWithoutPhysicalFiles(bool removeMyList = true)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        _logger.LogInformation("Remove Missing Files: Start");
        var seriesToUpdate = new HashSet<SVR_AnimeSeries>();
        using var session = _databaseFactory.SessionFactory.OpenSession();

        // remove missing files in valid import folders
        var filesAll = RepoFactory.VideoLocalPlace.GetAll()
            .Where(a => a.ImportFolder != null)
            .GroupBy(a => a.ImportFolder)
            .ToDictionary(a => a.Key, a => a.ToList());
        foreach (var vl in filesAll.Keys.SelectMany(a => filesAll[a]))
        {
            if (File.Exists(vl.FullServerPath)) continue;

            // delete video local record
            _logger.LogInformation("Removing Missing File: {ID}", vl.VideoLocalID);
            await _placeService.RemoveRecordWithOpenTransaction(session, vl, seriesToUpdate, removeMyList);
        }

        var videoLocalsAll = RepoFactory.VideoLocal.GetAll().ToList();
        // remove empty video locals
        BaseRepository.Lock(session, videoLocalsAll, (s, vls) =>
        {
            using var transaction = s.BeginTransaction();
            RepoFactory.VideoLocal.DeleteWithOpenTransaction(s, vls.Where(a => a.IsEmpty()).ToList());
            transaction.Commit();
        });

        // Remove duplicate video locals
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
            var from = values.Except(to).ToList();
            foreach (var places in from.Select(from => from.Places).Where(places => places != null && places.Count != 0))
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

            toRemove.AddRange(from);
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
#pragma warning disable CS0618
                        _logger.LogInformation("RemoveRecordsWithOrphanedImportFolder : {Filename}", v.FileName);
#pragma warning restore CS0618
                        seriesToUpdate.UnionWith(v.AnimeEpisodes.Select(a => a.AnimeSeries)
                            .DistinctBy(a => a.AnimeSeriesID));
                        RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(s, place);
                    }

                    transaction.Commit();
                });
            }

            // Remove duplicate places
            places = v.Places;
            if (places?.Count == 1) continue;

            if (places?.Count > 0)
            {
                places = places.DistinctBy(a => a.FullServerPath).ToList();
                places = v.Places?.Except(places).ToList() ?? [];
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
#pragma warning disable CS0618
            _logger.LogInformation("RemoveOrphanedVideoLocal : {Filename}", v.FileName);
#pragma warning restore CS0618
            seriesToUpdate.UnionWith(v.AnimeEpisodes.Select(a => a.AnimeSeries)
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

                        await scheduler.StartJob<DeleteFileFromMyListJob>(c =>
                        {
                            c.AnimeID = xref.AnimeID;
                            c.EpisodeType = ep.GetEpisodeTypeEnum();
                            c.EpisodeNumber = ep.EpisodeNumber;
                        });
                    }
                }
                else
                {
                    await scheduler.StartJob<DeleteFileFromMyListJob>(c =>
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
                        a.AniDBEpisode == null).ToArray();
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
        await Task.WhenAll(seriesToUpdate.Select(a => scheduler.StartJob<RefreshAnimeStatsJob>(b => b.AnimeID = a.AniDB_ID)));

        _logger.LogInformation("Remove Missing Files: Finished");
    }

    public async Task<string> DeleteImportFolder(int importFolderID, bool removeFromMyList = true)
    {
        try
        {
            var affectedSeries = new HashSet<SVR_AnimeSeries>();
            var vids = RepoFactory.VideoLocalPlace.GetByImportFolder(importFolderID);
            _logger.LogInformation("Deleting {VidsCount} video local records", vids.Count);
            using var session = _databaseFactory.SessionFactory.OpenSession();
            foreach (var vid in vids)
            {
                await _placeService.RemoveRecordWithOpenTransaction(session, vid, affectedSeries, removeFromMyList);
            }

            // delete the import folder
            RepoFactory.ImportFolder.Delete(importFolderID);

            var scheduler = await _schedulerFactory.GetScheduler();
            await Task.WhenAll(affectedSeries.Select(a => scheduler.StartJob<RefreshAnimeStatsJob>(b => b.AnimeID = a.AniDB_ID)));

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ex}", ex.ToString());
            return ex.Message;
        }
    }

    public async Task UpdateAllStats()
    {
        var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);
        await Task.WhenAll(RepoFactory.AnimeSeries.GetAll().Select(a => scheduler.StartJob<RefreshAnimeStatsJob>(b => b.AnimeID = a.AniDB_ID)));
    }

    public async Task<int> UpdateAniDBFileData(bool missingInfo, bool outOfDate, bool dryRun)
    {
        _logger.LogInformation("Updating Missing AniDB_File Info");
        var scheduler = await _schedulerFactory.GetScheduler();
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
            var anidbReleaseGroupIDs = RepoFactory.AniDB_ReleaseGroup.GetAll().Select(group => group.GroupID).ToHashSet();
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
            _logger.LogInformation("Queuing {Count} GetFile commands", vidsToUpdate.Count);
            foreach (var id in vidsToUpdate)
            {
                await scheduler.StartJob<GetAniDBFileJob>(c =>
                {
                    c.VideoLocalID = id;
                    c.ForceAniDB = true;
                });
            }

            _logger.LogInformation("Queuing {Count} GetReleaseGroup commands", groupsToUpdate.Count);
            foreach (var a in groupsToUpdate)
            {
                await scheduler.StartJob<GetAniDBReleaseGroupJob>(c => c.GroupID = a);
            }
        }

        return vidsToUpdate.Count;
    }

    public async Task CheckForUnreadNotifications(bool ignoreSchedule)
    {
        var settings = _settingsProvider.GetSettings();
        if (!ignoreSchedule && settings.AniDb.Notification_UpdateFrequency == ScheduledUpdateFrequency.Never) return;

        var schedule = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBNotify);
        if (schedule == null)
        {
            schedule = new()
            {
                UpdateType = (int)ScheduledUpdateType.AniDBNotify,
                UpdateDetails = string.Empty
            };
        }
        else
        {
            var freqHours = Utils.GetScheduledHours(settings.AniDb.Notification_UpdateFrequency);
            var tsLastRun = DateTime.Now - schedule.LastUpdate;

            // The NOTIFY command must not be issued more than once every 20 minutes according to the AniDB UDP API documentation:
            // https://wiki.anidb.net/UDP_API_Definition#NOTIFY:_Notifications
            // We will use 30 minutes as a safe interval.
            if (tsLastRun.TotalMinutes < 30) return;

            // if we have run this in the last freqHours and are not forcing it, then exit
            if (!ignoreSchedule && tsLastRun.TotalHours < freqHours) return;
        }

        schedule.LastUpdate = DateTime.Now;
        RepoFactory.ScheduledUpdate.Save(schedule);

        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<GetAniDBNotifyJob>();

        // process any unhandled moved file messages
        await RefreshAniDBMovedFiles(false);
    }

    public async Task RefreshAniDBMovedFiles(bool force)
    {
        var settings = _settingsProvider.GetSettings();
        if (force || settings.AniDb.Notification_HandleMovedFiles)
        {
            var messages = RepoFactory.AniDB_Message.GetUnhandledFileMoveMessages();
            if (messages.Count > 0)
            {
                var scheduler = await _schedulerFactory.GetScheduler();
                foreach (var msg in messages)
                {
                    await scheduler.StartJob<ProcessFileMovedMessageJob>(c => c.MessageID = msg.MessageID);
                }
            }
        }
    }

    public async Task CheckForCalendarUpdate(bool forceRefresh)
    {
        var settings = _settingsProvider.GetSettings();
        if (settings.AniDb.Calendar_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
        var scheduler = await _schedulerFactory.GetScheduler();

        var freqHours = Utils.GetScheduledHours(settings.AniDb.Calendar_UpdateFrequency);

        // update the calendar every 12 hours
        // we will always assume that an anime was downloaded via http first

        var schedule = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBCalendar);
        if (schedule != null)
        {
            // if we have run this in the last 12 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - schedule.LastUpdate;
            if (tsLastRun.TotalHours < freqHours && !forceRefresh) return;
        }

        await scheduler.StartJob<GetAniDBCalendarJob>(c => c.ForceRefresh = forceRefresh);
    }

    public async Task CheckForAnimeUpdate()
    {
        var settings = _settingsProvider.GetSettings();
        if (settings.AniDb.Anime_UpdateFrequency == ScheduledUpdateFrequency.Never) return;
        var scheduler = await _schedulerFactory.GetScheduler();

        var freqHours = Utils.GetScheduledHours(settings.AniDb.Anime_UpdateFrequency);

        // check for any updated anime info every 12 hours

        var schedule = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBUpdates);
        if (schedule != null)
        {
            // if we have run this in the last 12 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - schedule.LastUpdate;
            if (tsLastRun.TotalHours < freqHours) return;
        }

        await scheduler.StartJob<GetUpdatedAniDBAnimeJob>(c => c.ForceRefresh = true);
    }

    public async Task CheckForMyListSyncUpdate(bool forceRefresh)
    {
        var settings = _settingsProvider.GetSettings();
        if (settings.AniDb.MyList_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;

        var scheduler = await _schedulerFactory.GetScheduler();
        var freqHours = Utils.GetScheduledHours(settings.AniDb.MyList_UpdateFrequency);

        // update the calendar every 24 hours

        var schedule = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBMyListSync);
        if (schedule != null)
        {
            // if we have run this in the last 24 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - schedule.LastUpdate;
            _logger.LogTrace("Last AniDB MyList Sync: {Time} minutes ago", tsLastRun.TotalMinutes);
            if (tsLastRun.TotalHours < freqHours && !forceRefresh) return;
        }

        await scheduler.StartJob<SyncAniDBMyListJob>(c => c.ForceRefresh = forceRefresh);
    }

    public async Task CheckForTraktAllSeriesUpdate(bool forceRefresh)
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.TraktTv.Enabled) return;
        if (settings.TraktTv.UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;

        // update the calendar every xxx hours
        var schedule = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TraktUpdate);
        if (schedule == null)
        {
            schedule = new ScheduledUpdate
            {
                UpdateType = (int)ScheduledUpdateType.TraktUpdate,
                UpdateDetails = string.Empty
            };
        }
        else
        {
            var freqHours = Utils.GetScheduledHours(settings.TraktTv.UpdateFrequency);

            // if we have run this in the last xxx hours then exit
            var tsLastRun = DateTime.Now - schedule.LastUpdate;
            if (tsLastRun.TotalHours < freqHours && !forceRefresh) return;
        }

        schedule.LastUpdate = DateTime.Now;
        RepoFactory.ScheduledUpdate.Save(schedule);

        var scheduler = await _schedulerFactory.GetScheduler();

        // update all info
        var allCrossRefs = RepoFactory.CrossRef_AniDB_TraktV2.GetAll();
        foreach (var xref in allCrossRefs)
        {
            scheduler.StartJob<GetTraktSeriesJob>(c => c.TraktID = xref.TraktID).GetAwaiter().GetResult();
        }

        // scan for new matches
        _traktHelper.ScanForMatches();
    }

    public void CheckForTraktTokenUpdate(bool forceRefresh)
    {
        try
        {
            var settings = _settingsProvider.GetSettings();
            if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.TokenExpirationDate)) return;

            // Convert the Unix timestamp to DateTime directly
            var expirationDate = DateTimeOffset.FromUnixTimeSeconds(long.Parse(settings.TraktTv.TokenExpirationDate)).DateTime;

            // Check if force refresh is requested or the token is expired
            if (forceRefresh || DateTime.Now.Add(TimeSpan.FromDays(45)) >= expirationDate)
            {
                _traktHelper.RefreshAuthToken();

                // Update the last token refresh timestamp
                var schedule = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TraktToken)
                            ?? new ScheduledUpdate { UpdateType = (int)ScheduledUpdateType.TraktToken, UpdateDetails = string.Empty };

                schedule.LastUpdate = DateTime.Now;
                RepoFactory.ScheduledUpdate.Save(schedule);

                _logger.LogInformation("Trakt token refreshed successfully. Expiry date: {Date}", expirationDate);
            }
            else
            {
                _logger.LogInformation("Trakt token is still valid. Expiry date: {Date}", expirationDate);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CheckForTraktTokenUpdate: {Ex}", ex);
        }
    }

    public async Task CheckForAniDBFileUpdate(bool forceRefresh)
    {
        var settings = _settingsProvider.GetSettings();
        if (settings.AniDb.File_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh)
        {
            return;
        }

        var freqHours = Utils.GetScheduledHours(settings.AniDb.File_UpdateFrequency);
        var scheduler = await _schedulerFactory.GetScheduler();

        // check for any updated anime info every 12 hours

        var schedule = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBFileUpdates);
        if (schedule != null)
        {
            // if we have run this in the last 12 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - schedule.LastUpdate;
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

            await scheduler.StartJob<ProcessFileJob>(c =>
                {
                    c.VideoLocalID = vl.VideoLocalID;
                    c.ForceAniDB = true;
                }
            );
        }

        // now check for any files which have been manually linked and are less than 30 days old


        schedule ??= new ScheduledUpdate
        {
            UpdateType = (int)ScheduledUpdateType.AniDBFileUpdates,
            UpdateDetails = string.Empty
        };

        schedule.LastUpdate = DateTime.Now;
        RepoFactory.ScheduledUpdate.Save(schedule);
    }

    public void CheckForPreviouslyIgnored()
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

                    if (resultVideoLocalsIgnored.Count != 0)
                    {
                        vl.IsIgnored = true;
                        RepoFactory.VideoLocal.Save(vl, false);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CheckForPreviouslyIgnored: {Ex}", ex);
        }
    }

    public async Task ScheduleMissingAnidbCreators()
    {
        if (!_settingsProvider.GetSettings().AniDb.DownloadCreators) return;

        var allCreators = RepoFactory.AniDB_Creator.GetAll();
        var allMissingCreators = RepoFactory.AnimeStaff.GetAll()
            .Select(s => s.AniDBID)
            .Distinct()
            .Except(allCreators.Select(a => a.CreatorID))
            .ToList();
        var missingCount = allMissingCreators.Count;
        allMissingCreators.AddRange(
            allCreators
                .Where(creator => creator.Type is Providers.AniDB.CreatorType.Unknown)
                .Select(creator => creator.CreatorID)
                .Distinct()
        );
        var partiallyMissingCount = allMissingCreators.Count - missingCount;

        var startedAt = DateTime.Now;
        _logger.LogInformation("Scheduling {Count} AniDB Creators for a refresh. (Missing={MissingCount},PartiallyMissing={PartiallyMissingCount},Total={Total})", allMissingCreators.Count, missingCount, partiallyMissingCount, allMissingCreators.Count);
        var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);
        var progressCount = 0;
        foreach (var creatorID in allMissingCreators)
        {
            await scheduler.StartJob<GetAniDBCreatorJob>(c => c.CreatorID = creatorID).ConfigureAwait(false);

            if (++progressCount % 10 == 0)
                _logger.LogInformation("Scheduling {Count} AniDB Creators for a refresh. (Progress={Count}/{Total})", allMissingCreators.Count, progressCount, allMissingCreators.Count);
        }

        _logger.LogInformation("Scheduled {Count} AniDB Creators in {TimeSpan}", allMissingCreators.Count, DateTime.Now - startedAt);
    }
}
