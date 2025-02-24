using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentNHibernate.Utils;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
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

public class ActionService(
    ILogger<ActionService> _logger,
    ISchedulerFactory _schedulerFactory,
    IRequestFactory _requestFactory,
    ISettingsProvider _settingsProvider,
    VideoLocalService _videoService,
    VideoLocal_PlaceService _placeService,
    TmdbMetadataService _tmdbService,
    AnimeSeriesService _seriesService,
    TraktTVHelper _traktHelper,
    DatabaseFactory _databaseFactory,
    HttpXmlUtils _xmlUtils
)
{
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
                var matchAttempts = RepoFactory.StoredReleaseInfo_MatchAttempt.GetByEd2kAndFileSize(vl.Hash, vl.FileSize).Count;
                if (matchAttempts > settings.Import.MaxAutoScanAttemptsPerFile)
                    continue;
            }

            await scheduler.StartJob<ProcessFileJob>(
                c =>
                {
                    c.VideoLocalID = vl.VideoLocalID;
                    c.ForceRecheck = true;
                }
            );
        }

        // check that all the episode data is populated
        foreach (var vl in RepoFactory.VideoLocal.GetVideosWithMissingCrossReferenceData())
        {
            // queue scan for files that are automatically linked but missing AniDB_File data
            await scheduler.StartJob<ProcessFileJob>(c => c.VideoLocalID = vl.VideoLocalID);
        }
    }

    public Task RunImport_ScanFolder(int importFolderID, bool skipMyList = false)
        => RunImport_DetectFiles(skipMyList: skipMyList, importFolderIDs: [importFolderID]);

    public async Task RunImport_DetectFiles(bool onlyNewFiles = false, bool onlyInSourceFolders = false, bool skipMyList = false, IEnumerable<int> importFolderIDs = null)
    {
        IReadOnlyList<SVR_ImportFolder> importFolders;
        IEnumerable<SVR_VideoLocal_Place> locationsToCheck;
        if (importFolderIDs is null)
        {
            importFolders = RepoFactory.ImportFolder.GetAll();
            locationsToCheck = RepoFactory.VideoLocalPlace.GetAll();
        }
        else
        {
            importFolders = importFolderIDs
                .Select(RepoFactory.ImportFolder.GetByID)
                .WhereNotNull()
                .ToList();
            locationsToCheck = importFolders.SelectMany(a => a.Places);
        }
        if (importFolders.Count is 0)
            return;

        var existingFiles = new HashSet<string>();
        foreach (var location in locationsToCheck)
        {
            try
            {
                if (location.FullServerPath is not { Length: > 0 } path)
                {
                    _logger.LogInformation("Removing invalid full path for VideoLocal_Place; {Path} (Video={VideoID},Place={PlaceID},ImportFolder={ImportFolderID})", location.FilePath, location.VideoLocalID, location.VideoLocal_Place_ID, location.ImportFolderID);
                    await _placeService.RemoveRecord(location);
                    continue;
                }

                existingFiles.Add(path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An exception occurred while processing VideoLocal_Place; {Path} (Video={VideoID},Place={PlaceID},ImportFolder={ImportFolderID})", location.FilePath, location.VideoLocalID, location.VideoLocal_Place_ID, location.ImportFolderID);
            }
        }

        var filesFound = 0;
        var videosFound = 0;
        var ignoredFiles = RepoFactory.VideoLocal.GetIgnoredVideos()
            .SelectMany(a => a.Places)
            .Select(a => a.FullServerPath)
            .Where(a => !string.IsNullOrEmpty(a))
            .ToList();
        var settings = _settingsProvider.GetSettings();
        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var folder in importFolders)
        {
            if (onlyInSourceFolders && !folder.FolderIsDropSource)
                continue;

            var files = folder.Files
                .Where(fileName =>
                {
                    if (settings.Import.Exclude.Any(s => Regex.IsMatch(fileName, s)))
                    {
                        _logger.LogTrace("Import exclusion, skipping --- {Name}", fileName);
                        return false;
                    }

                    return !onlyNewFiles || !existingFiles.Contains(fileName);
                })
                .Except(ignoredFiles, StringComparer.InvariantCultureIgnoreCase)
                .ToList();
            var total = files.Count;
            foreach (var fileName in files)
            {
                if (++filesFound % 100 == 0 || filesFound == 1 || filesFound == total)
                    _logger.LogTrace("Processing File {Count}/{Total} in folder {FolderName} --- {Name}", filesFound, total, folder.ImportFolderName, fileName);

                if (!Utils.IsVideo(fileName))
                    continue;

                videosFound++;
                if (!existingFiles.Contains(fileName))
                    ShokoEventHandler.Instance.OnFileDetected(folder, new FileInfo(fileName));

                await scheduler.StartJob<DiscoverFileJob>(a =>
                {
                    a.FilePath = fileName;
                    a.SkipMyList = skipMyList;
                });
            }
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

            var entities = RepoFactory.TMDB_Image_Entity.GetByRemoteFileName(image.RemoteFileName)
                .Where(x => x.ImageType == type)
                .ToList();
            foreach (var entity in entities)
                switch (entity.TmdbEntityType)
                {
                    case ForeignEntityType.Movie:
                        if (countsForMovies.ContainsKey(entity.TmdbEntityID))
                            countsForMovies[entity.TmdbEntityID] += 1;
                        else
                            countsForMovies[entity.TmdbEntityID] = 1;
                        break;
                    case ForeignEntityType.Episode:
                        if (countForEpisodes.ContainsKey(entity.TmdbEntityID))
                            countForEpisodes[entity.TmdbEntityID] += 1;
                        else
                            countForEpisodes[entity.TmdbEntityID] = 1;
                        break;
                    case ForeignEntityType.Season:
                        if (countForSeasons.ContainsKey(entity.TmdbEntityID))
                            countForSeasons[entity.TmdbEntityID] += 1;
                        else
                            countForSeasons[entity.TmdbEntityID] = 1;
                        break;
                    case ForeignEntityType.Show:
                        if (countForShows.ContainsKey(entity.TmdbEntityID))
                            countForShows[entity.TmdbEntityID] += 1;
                        else
                            countForShows[entity.TmdbEntityID] = 1;
                        break;
                    case ForeignEntityType.Collection:
                        if (countForCollections.ContainsKey(entity.TmdbEntityID))
                            countForCollections[entity.TmdbEntityID] += 1;
                        else
                            countForCollections[entity.TmdbEntityID] = 1;
                        break;
                    case ForeignEntityType.Network:
                        if (countForNetworks.ContainsKey(entity.TmdbEntityID))
                            countForNetworks[entity.TmdbEntityID] += 1;
                        else
                            countForNetworks[entity.TmdbEntityID] = 1;
                        break;
                    case ForeignEntityType.Company:
                        if (countForCompanies.ContainsKey(entity.TmdbEntityID))
                            countForCompanies[entity.TmdbEntityID] += 1;
                        else
                            countForCompanies[entity.TmdbEntityID] = 1;
                        break;
                    case ForeignEntityType.Person:
                        if (countForPersons.ContainsKey(entity.TmdbEntityID))
                            countForPersons[entity.TmdbEntityID] += 1;
                        else
                            countForPersons[entity.TmdbEntityID] = 1;
                        break;
                }
        }

        var scheduler = await schedulerFactory.GetScheduler();
        foreach (var image in allImages)
        {
            var path = image.LocalPath;
            if (string.IsNullOrEmpty(path) || File.Exists(path))
                continue;

            // Check if we should download the image or not.
            var limitEnabled = maxCount > 0;
            var entities = RepoFactory.TMDB_Image_Entity.GetByRemoteFileName(image.RemoteFileName)
                .Where(x => x.ImageType == type)
                .ToList();
            var shouldDownload = !limitEnabled && entities.Count > 0;
            if (limitEnabled && entities.Count > 0)
                foreach (var entity in entities)
                    switch (entity.TmdbEntityType)
                    {
                        case ForeignEntityType.Movie:
                            if (countsForMovies.ContainsKey(entity.TmdbEntityID) && countsForMovies[entity.TmdbEntityID] < maxCount)
                                shouldDownload = true;
                            break;
                        case ForeignEntityType.Episode:
                            if (countForEpisodes.ContainsKey(entity.TmdbEntityID) && countForEpisodes[entity.TmdbEntityID] < maxCount)
                                shouldDownload = true;
                            break;
                        case ForeignEntityType.Season:
                            if (countForSeasons.ContainsKey(entity.TmdbEntityID) && countForSeasons[entity.TmdbEntityID] < maxCount)
                                shouldDownload = true;
                            break;
                        case ForeignEntityType.Show:
                            if (countForShows.ContainsKey(entity.TmdbEntityID) && countForShows[entity.TmdbEntityID] < maxCount)
                                shouldDownload = true;
                            break;
                        case ForeignEntityType.Collection:
                            if (countForCollections.ContainsKey(entity.TmdbEntityID) && countForCollections[entity.TmdbEntityID] < maxCount)
                                shouldDownload = true;
                            break;
                        case ForeignEntityType.Network:
                            if (countForNetworks.ContainsKey(entity.TmdbEntityID) && countForNetworks[entity.TmdbEntityID] < maxCount)
                                shouldDownload = true;
                            break;
                        case ForeignEntityType.Company:
                            if (countForCompanies.ContainsKey(entity.TmdbEntityID) && countForCompanies[entity.TmdbEntityID] < maxCount)
                                shouldDownload = true;
                            break;
                        case ForeignEntityType.Person:
                            if (countForPersons.ContainsKey(entity.TmdbEntityID) && countForPersons[entity.TmdbEntityID] < maxCount)
                                shouldDownload = true;
                            break;
                    }

            if (shouldDownload)
            {
                await scheduler.StartJob<DownloadTmdbImageJob>(c =>
                {
                    c.ImageID = image.TMDB_ImageID;
                    c.ImageType = image.ImageType;
                });

                foreach (var entity in entities)
                    switch (entity.TmdbEntityType)
                    {
                        case ForeignEntityType.Movie:
                            if (countsForMovies.ContainsKey(entity.TmdbEntityID))
                                countsForMovies[entity.TmdbEntityID] += 1;
                            else
                                countsForMovies[entity.TmdbEntityID] = 1;
                            break;
                        case ForeignEntityType.Episode:
                            if (countForEpisodes.ContainsKey(entity.TmdbEntityID))
                                countForEpisodes[entity.TmdbEntityID] += 1;
                            else
                                countForEpisodes[entity.TmdbEntityID] = 1;
                            break;
                        case ForeignEntityType.Season:
                            if (countForSeasons.ContainsKey(entity.TmdbEntityID))
                                countForSeasons[entity.TmdbEntityID] += 1;
                            else
                                countForSeasons[entity.TmdbEntityID] = 1;
                            break;
                        case ForeignEntityType.Show:
                            if (countForShows.ContainsKey(entity.TmdbEntityID))
                                countForShows[entity.TmdbEntityID] += 1;
                            else
                                countForShows[entity.TmdbEntityID] = 1;
                            break;
                        case ForeignEntityType.Collection:
                            if (countForCollections.ContainsKey(entity.TmdbEntityID))
                                countForCollections[entity.TmdbEntityID] += 1;
                            else
                                countForCollections[entity.TmdbEntityID] = 1;
                            break;
                        case ForeignEntityType.Network:
                            if (countForNetworks.ContainsKey(entity.TmdbEntityID))
                                countForNetworks[entity.TmdbEntityID] += 1;
                            else
                                countForNetworks[entity.TmdbEntityID] = 1;
                            break;
                        case ForeignEntityType.Company:
                            if (countForCompanies.ContainsKey(entity.TmdbEntityID))
                                countForCompanies[entity.TmdbEntityID] += 1;
                            else
                                countForCompanies[entity.TmdbEntityID] = 1;
                            break;
                        case ForeignEntityType.Person:
                            if (countForPersons.ContainsKey(entity.TmdbEntityID))
                                countForPersons[entity.TmdbEntityID] += 1;
                            else
                                countForPersons[entity.TmdbEntityID] = 1;
                            break;
                    }
            }
        }
    }

    private static bool ShouldUpdateAniDBCreatorImages(IServerSettings settings, SVR_AniDB_Anime anime)
    {
        if (!settings.AniDb.DownloadCreators) return false;

        foreach (var creator in RepoFactory.AniDB_Anime_Character_Creator.GetByAnimeID(anime.AnimeID).Select(a => a.Creator).WhereNotNull())
        {
            if (string.IsNullOrEmpty(creator.ImagePath)) continue;
            if (!ImageExtensions.IsImageValid(creator.GetFullImagePath())) return true;
        }

        foreach (var creator in RepoFactory.AniDB_Anime_Staff.GetByAnimeID(anime.AnimeID).Select(a => RepoFactory.AniDB_Creator.GetByCreatorID(a.CreatorID)).WhereNotNull())
        {
            if (string.IsNullOrEmpty(creator.ImagePath)) continue;
            if (!ImageExtensions.IsImageValid(creator.GetFullImagePath())) return true;
        }

        return false;
    }

    private static bool ShouldUpdateAniDBCharacterImages(IServerSettings settings, SVR_AniDB_Anime anime)
    {
        if (!settings.AniDb.DownloadCharacters) return false;

        foreach (var chr in RepoFactory.AniDB_Character.GetCharactersForAnime(anime.AnimeID))
        {
            if (string.IsNullOrEmpty(chr.ImagePath)) continue;
            if (!ImageExtensions.IsImageValid(chr.GetFullImagePath())) return true;
        }

        return false;
    }

    public void RunImport_ScanTrakt()
        => _traktHelper.ScanForMatches();

    public Task RunImport_ScanTMDB()
        => _tmdbService.ScanForMatches();

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
                c.DownloadRelations = false;
                c.CreateSeriesEntry = false;
                c.SkipTmdbUpdate = true;
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
                await _videoService.ScheduleRemovalFromMyList(v);

            BaseRepository.Lock(session, v, (s, vl) =>
            {
                using var transaction = s.BeginTransaction();
                RepoFactory.VideoLocal.DeleteWithOpenTransaction(s, vl);
                transaction.Commit();
            });
        }

        // Clean up failed imports
        var list = RepoFactory.VideoLocal.GetAll()
            .SelectMany(a => a.EpisodeCrossReferences)
            .Where(a => a.AniDBAnime == null || a.AniDBEpisode == null)
            .ToArray();
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

    public async Task<int> UpdateAnidbReleaseInfo(bool countOnly = false)
    {
        _logger.LogInformation("Updating Missing AniDB_File Info");
        var incorrectGroups = RepoFactory.StoredReleaseInfo.GetAll()
            .Where(r =>
                !string.IsNullOrEmpty(r.GroupID) &&
                r.GroupSource is "AniDB" &&
                int.TryParse(r.GroupID, out var groupID) && (
                    string.IsNullOrEmpty(r.GroupName) ||
                    string.IsNullOrEmpty(r.GroupShortName)
                )
            )
            .DistinctBy(a => a.GroupID)
            .Select(a => int.Parse(a.GroupID))
            .ToHashSet();
        var missingFiles = RepoFactory.StoredReleaseInfo.GetAll()
            .Where(r => r.ProviderName is "AniDB" && (string.IsNullOrEmpty(r.GroupID) || r.GroupSource is not "AniDB"))
            .Select(a => RepoFactory.VideoLocal.GetByEd2kAndSize(a.ED2K, a.FileSize))
            .WhereNotNull()
            .Select(a => a.VideoLocalID)
            .ToList();

        if (!countOnly)
        {
            var scheduler = await _schedulerFactory.GetScheduler();

            _logger.LogInformation("Queuing {Count} GetFile commands", missingFiles.Count);
            foreach (var id in missingFiles)
            {
                await scheduler.StartJob<ProcessFileJob>(c =>
                {
                    c.VideoLocalID = id;
                    c.ForceRecheck = true;
                });
            }

            _logger.LogInformation("Queuing {Count} GetReleaseGroup commands", incorrectGroups.Count);
            foreach (var a in incorrectGroups)
            {
                await scheduler.StartJob<GetAniDBReleaseGroupJob>(c => c.GroupID = a);
            }
        }

        return missingFiles.Count;
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
                var matchAttempts = RepoFactory.StoredReleaseInfo_MatchAttempt.GetByEd2kAndFileSize(vl.Hash, vl.FileSize).Count;
                if (matchAttempts > settings.Import.MaxAutoScanAttemptsPerFile)
                    continue;
            }

            await scheduler.StartJob<ProcessFileJob>(c =>
                {
                    c.VideoLocalID = vl.VideoLocalID;
                    c.ForceRecheck = true;
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

    public async Task DownloadMissingAnidbAnimeXmls()
    {
        // Check existing anime.
        var index = 0;
        var queuedAnimeSet = new HashSet<int>();
        var localAnimeSet = RepoFactory.AniDB_Anime.GetAll()
            .Select(a => a.AnimeID)
            .OrderBy(a => a)
            .ToHashSet();
        _logger.LogInformation("Checking {AllAnimeCount} anime for missing XML files…", localAnimeSet.Count);
        foreach (var animeID in localAnimeSet)
        {
            if (++index % 10 == 1 || index == localAnimeSet.Count)
                _logger.LogInformation("Checking {AllAnimeCount} anime for missing XML files — {CurrentCount}/{AllAnimeCount}", localAnimeSet.Count, index + 1, localAnimeSet.Count);

            var rawXml = await _xmlUtils.LoadAnimeHTTPFromFile(animeID);
            if (rawXml != null)
                continue;

            _logger.LogDebug("Found anime {AnimeID} with missing XML", animeID);
            await _seriesService.QueueAniDBRefresh(animeID, true, false, false, skipTmdbUpdate: true);
            queuedAnimeSet.Add(animeID);
        }
    }

    public async Task ScheduleMissingAnidbAnimeForFiles()
    {
        // Attempt to fix cross-references with incomplete data.
        var index = 0;
        var videos = RepoFactory.VideoLocal.GetVideosWithMissingCrossReferenceData();
        var unknownEpisodeDict = videos
            .SelectMany(file => file.EpisodeCrossReferences)
            .Where(xref => xref.AnimeID is 0)
            .GroupBy(xref => xref.EpisodeID)
            .ToDictionary(groupBy => groupBy.Key, groupBy => groupBy.ToList());
        _logger.LogInformation("Attempting to fix {MissingAnimeCount} cross-references with unknown anime…", unknownEpisodeDict.Count);
        foreach (var (episodeId, xrefs) in unknownEpisodeDict)
        {
            if (++index % 10 == 1)
                _logger.LogInformation("Attempting to fix {MissingAnimeCount} cross-references with unknown anime — {CurrentCount}/{MissingAnimeCount}", unknownEpisodeDict.Count, index + 1, unknownEpisodeDict.Count);

            var episode = RepoFactory.AniDB_Episode.GetByEpisodeID(episodeId);
            if (episode is not null)
            {
                foreach (var xref in xrefs)
                    xref.AnimeID = episode.AnimeID;
                RepoFactory.CrossRef_File_Episode.Save(xrefs);
                continue;
            }

            int? epAnimeID = null;
            var epRequest = _requestFactory.Create<RequestGetEpisode>(r => r.EpisodeID = episodeId);
            try
            {
                var epResponse = epRequest.Send();
                epAnimeID = epResponse.Response?.AnimeID;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not get Episode Info for {EpisodeID}", episode.EpisodeID);
            }

            if (epAnimeID is not null)
            {
                foreach (var xref in xrefs)
                    xref.AnimeID = epAnimeID.Value;
                RepoFactory.CrossRef_File_Episode.Save(xrefs);
            }
        }

        // Queue missing anime needed by existing files.
        index = 0;
        var localAnimeSet = RepoFactory.AniDB_Anime.GetAll()
            .Select(a => a.AnimeID)
            .OrderBy(a => a)
            .ToHashSet();
        var localEpisodeSet = RepoFactory.AniDB_Episode.GetAll()
            .Select(episode => episode.EpisodeID)
            .ToHashSet();
        var missingAnimeSet = videos
            .SelectMany(file => file.EpisodeCrossReferences)
            .Where(xref => xref.AnimeID > 0 && (!localAnimeSet.Contains(xref.AnimeID) || !localEpisodeSet.Contains(xref.EpisodeID)))
            .Select(xref => xref.AnimeID)
            .ToHashSet();
        var settings = _settingsProvider.GetSettings();
        _logger.LogInformation("Queueing {MissingAnimeCount} anime that needs an update…", missingAnimeSet.Count);
        foreach (var animeID in missingAnimeSet)
        {
            if (++index % 10 == 1 || index == missingAnimeSet.Count)
                _logger.LogInformation("Queueing {MissingAnimeCount} anime that needs an update — {CurrentCount}/{MissingAnimeCount}", missingAnimeSet.Count, index + 1, missingAnimeSet.Count);

            await _seriesService.QueueAniDBRefresh(animeID, false, settings.AniDb.DownloadRelatedAnime, true);
        }
    }

    public async Task ScheduleMissingAnidbCreators()
    {
        if (!_settingsProvider.GetSettings().AniDb.DownloadCreators) return;

        var allCreators = RepoFactory.AniDB_Creator.GetAll();
        var allMissingCreators = allCreators
                .Where(creator => creator.Type is Providers.AniDB.CreatorType.Unknown)
                .Select(creator => creator.CreatorID)
                .Distinct()
                .ToList();

        var startedAt = DateTime.Now;
        _logger.LogInformation("Scheduling {Count} AniDB Creators for a refresh.", allMissingCreators.Count);
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
