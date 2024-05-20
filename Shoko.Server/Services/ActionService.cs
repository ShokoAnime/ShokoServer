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
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.FileHelper;
using Shoko.Server.Models;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Scheduling.Jobs.TMDB;
using Shoko.Server.Scheduling.Jobs.Trakt;
using Shoko.Server.Scheduling.Jobs.TvDB;
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
    private readonly MovieDBHelper _movieDBHelper;
    private readonly TvDBApiHelper _tvdbHelper;
    private readonly TraktTVHelper _traktHelper;

    public ActionService(ILogger<ActionService> logger, ISchedulerFactory schedulerFactory, ISettingsProvider settingsProvider, VideoLocal_PlaceService placeService, TvDBApiHelper tvdbHelper, TraktTVHelper traktHelper, MovieDBHelper movieDBHelper)
    {
        _logger = logger;
        _schedulerFactory = schedulerFactory;
        _settingsProvider = settingsProvider;
        _placeService = placeService;
        _tvdbHelper = tvdbHelper;
        _traktHelper = traktHelper;
        _movieDBHelper = movieDBHelper;
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
            var p = vl.GetBestVideoLocalPlace(true);
            if (p == null) continue;

            await scheduler.StartJob<HashFileJob>(c => c.FilePath = p.FullServerPath);
        }

        foreach (var vl in filesToHash)
        {
            // don't use if it is in the previous list
            if (dictFilesToHash.ContainsKey(vl.VideoLocalID)) continue;

            try
            {
                var p = vl.GetBestVideoLocalPlace(true);
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
            var fldr = RepoFactory.ImportFolder.GetByID(importFolderID);
            if (fldr == null) return;

            // first build a list of files that we already know about, as we don't want to process them again
            var filesAll = RepoFactory.VideoLocalPlace.GetByImportFolder(fldr.ImportFolderID);
            var dictFilesExisting = new Dictionary<string, SVR_VideoLocal_Place>();
            foreach (var vl in filesAll.Where(a => a.FullServerPath != null))
            {
                dictFilesExisting[vl.FullServerPath] = vl;
            }

            Utils.GetFilesForImportFolder(fldr.BaseDirectory, ref fileList);

            // Get Ignored Files and remove them from the scan listing
            var ignoredFiles = RepoFactory.VideoLocal.GetIgnoredVideos().SelectMany(a => a.Places)
                .Select(a => a.FullServerPath).Where(a => !string.IsNullOrEmpty(a)).ToList();
            fileList = fileList.Except(ignoredFiles, StringComparer.InvariantCultureIgnoreCase).ToList();

            // get a list of all files in the share
            foreach (var fileName in fileList)
            {
                i++;

                if (dictFilesExisting.TryGetValue(fileName, out var value) && fldr.IsDropSource == 1) _placeService.RenameAndMoveAsRequired(value);

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
            _logger.LogError(ex, ex.ToString());
        }
    }

    public async Task RunImport_DropFolders()
    {
        var settings = _settingsProvider.GetSettings();
        var scheduler = await _schedulerFactory.GetScheduler();
        // get a complete list of files
        var fileList = new List<string>();
        foreach (var share in RepoFactory.ImportFolder.GetAll())
        {
            if (!share.FolderIsDropSource) continue;
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
                _logger.LogError(ex, ex.ToString());
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

            var tup = VideoLocal_PlaceRepository.GetFromFullPath(fileName);
            ShokoEventHandler.Instance.OnFileDetected(tup.Item1, new FileInfo(fileName));

            await scheduler.StartJob<DiscoverFileJob>(a => a.FilePath = fileName);
        }

        _logger.LogDebug("Found {Count} files", filesFound);
        _logger.LogDebug("Found {Count} videos", videosFound);
    }

    public async Task RunImport_GetImages()
    {
        var settings = _settingsProvider.GetSettings();
        var scheduler = await _schedulerFactory.GetScheduler();
        // AniDB posters
        foreach (var anime in RepoFactory.AniDB_Anime.GetAll())
        {
            if (string.IsNullOrEmpty(anime.PosterPath)) continue;
            var fileExists = File.Exists(anime.PosterPath);
            if (fileExists) continue;

            await scheduler.StartJob<GetAniDBImagesJob>(c => c.AnimeID = anime.AnimeID);
        }

        // TvDB Posters
        if (settings.TvDB.AutoPosters)
        {
            var postersCount = new Dictionary<int, int>();

            // build a dictionary of series and how many images exist
            var allPosters = RepoFactory.TvDB_ImagePoster.GetAll();
            foreach (var tvPoster in allPosters)
            {
                if (string.IsNullOrEmpty(tvPoster.GetFullImagePath())) continue;
                var fileExists = File.Exists(tvPoster.GetFullImagePath());
                if (!fileExists) continue;
                if (!postersCount.TryAdd(tvPoster.SeriesID, 1)) postersCount[tvPoster.SeriesID] += 1;
            }

            foreach (var tvPoster in allPosters)
            {
                if (string.IsNullOrEmpty(tvPoster.GetFullImagePath())) continue;
                var fileExists = File.Exists(tvPoster.GetFullImagePath());
                var postersAvailable = 0;
                if (postersCount.TryGetValue(tvPoster.SeriesID, out var value)) postersAvailable = value;

                if (fileExists || postersAvailable >= settings.TvDB.AutoPostersAmount) continue;

                await scheduler.StartJob<DownloadTvDBImageJob>(c =>
                    {
                        c.Anime = RepoFactory.TvDB_Series.GetByTvDBID(tvPoster.SeriesID)?.SeriesName;
                        c.ImageID = tvPoster.TvDB_ImagePosterID;
                        c.ImageType = ImageEntityType.TvDB_Cover;
                    }
                );

                if (!postersCount.TryAdd(tvPoster.SeriesID, 1)) postersCount[tvPoster.SeriesID] += 1;
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
                if (string.IsNullOrEmpty(tvFanart.GetFullImagePath())) continue;
                var fileExists = File.Exists(tvFanart.GetFullImagePath());
                if (!fileExists) continue;
                if (!fanartCount.TryAdd(tvFanart.SeriesID, 1)) fanartCount[tvFanart.SeriesID] += 1;
            }

            foreach (var tvFanart in allFanart)
            {
                if (string.IsNullOrEmpty(tvFanart.GetFullImagePath())) continue;
                var fileExists = File.Exists(tvFanart.GetFullImagePath());

                var fanartAvailable = 0;
                if (fanartCount.TryGetValue(tvFanart.SeriesID, out var value)) fanartAvailable = value;
                if (fileExists || fanartAvailable >= settings.TvDB.AutoFanartAmount) continue;

                await scheduler.StartJob<DownloadTvDBImageJob>(c =>
                    {
                        c.Anime = RepoFactory.TvDB_Series.GetByTvDBID(tvFanart.SeriesID)?.SeriesName;
                        c.ImageID = tvFanart.TvDB_ImageFanartID;
                        c.ImageType = ImageEntityType.TvDB_FanArt;
                    }
                );

                if (!fanartCount.TryAdd(tvFanart.SeriesID, 1)) fanartCount[tvFanart.SeriesID] += 1;
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
                if (string.IsNullOrEmpty(tvBanner.GetFullImagePath())) continue;
                var fileExists = File.Exists(tvBanner.GetFullImagePath());
                if (!fileExists) continue;
                if (!fanartCount.TryAdd(tvBanner.SeriesID, 1)) fanartCount[tvBanner.SeriesID] += 1;
            }

            foreach (var tvBanner in allBanners)
            {
                if (string.IsNullOrEmpty(tvBanner.GetFullImagePath())) continue;
                var fileExists = File.Exists(tvBanner.GetFullImagePath());
                var bannersAvailable = 0;
                if (fanartCount.TryGetValue(tvBanner.SeriesID, out var value)) bannersAvailable = value;
                if (fileExists || bannersAvailable >= settings.TvDB.AutoWideBannersAmount) continue;

                await scheduler.StartJob<DownloadTvDBImageJob>(c =>
                    {
                        c.Anime = RepoFactory.TvDB_Series.GetByTvDBID(tvBanner.SeriesID)?.SeriesName;
                        c.ImageID = tvBanner.TvDB_ImageWideBannerID;
                        c.ImageType = ImageEntityType.TvDB_Banner;
                    }
                );

                if (!fanartCount.TryAdd(tvBanner.SeriesID, 1)) fanartCount[tvBanner.SeriesID] += 1;
            }
        }

        // TvDB Episodes

        foreach (var tvEpisode in RepoFactory.TvDB_Episode.GetAll())
        {
            if (string.IsNullOrEmpty(tvEpisode.GetFullImagePath())) continue;
            var fileExists = File.Exists(tvEpisode.GetFullImagePath());
            if (fileExists) continue;

            await scheduler.StartJob<DownloadTvDBImageJob>(c =>
                {
                    c.Anime = RepoFactory.TvDB_Series.GetByTvDBID(tvEpisode.SeriesID)?.SeriesName;
                    c.ImageID = tvEpisode.TvDB_EpisodeID;
                    c.ImageType = ImageEntityType.TvDB_Episode;
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
                if (string.IsNullOrEmpty(moviePoster.GetFullImagePath())) continue;
                var fileExists = File.Exists(moviePoster.GetFullImagePath());
                if (!fileExists) continue;
                if (!postersCount.TryAdd(moviePoster.MovieId, 1)) postersCount[moviePoster.MovieId] += 1;
            }

            foreach (var moviePoster in allPosters)
            {
                if (string.IsNullOrEmpty(moviePoster.GetFullImagePath())) continue;
                var fileExists = File.Exists(moviePoster.GetFullImagePath());
                var postersAvailable = 0;
                if (postersCount.TryGetValue(moviePoster.MovieId, out var value)) postersAvailable = value;

                if (fileExists || postersAvailable >= settings.MovieDb.AutoPostersAmount) continue;

                await scheduler.StartJob<DownloadTMDBImageJob>(c =>
                    {
                        c.ImageID = moviePoster.MovieDB_PosterID;
                        c.ImageType = ImageEntityType.MovieDB_Poster;
                    }
                );

                if (!postersCount.TryAdd(moviePoster.MovieId, 1)) postersCount[moviePoster.MovieId] += 1;
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
                if (string.IsNullOrEmpty(movieFanart.GetFullImagePath())) continue;
                var fileExists = File.Exists(movieFanart.GetFullImagePath());
                if (!fileExists) continue;
                if (!fanartCount.TryAdd(movieFanart.MovieId, 1)) fanartCount[movieFanart.MovieId] += 1;
            }

            foreach (var movieFanart in RepoFactory.MovieDB_Fanart.GetAll())
            {
                if (string.IsNullOrEmpty(movieFanart.GetFullImagePath())) continue;
                var fileExists = File.Exists(movieFanart.GetFullImagePath());
                var fanartAvailable = 0;
                if (fanartCount.TryGetValue(movieFanart.MovieId, out var value)) fanartAvailable = value;
                if (fileExists || fanartAvailable >= settings.MovieDb.AutoFanartAmount) continue;

                await scheduler.StartJob<DownloadTMDBImageJob>(c =>
                    {
                        c.ImageID = movieFanart.MovieDB_FanartID;
                        c.ImageType = ImageEntityType.MovieDB_FanArt;
                    }
                );

                if (!fanartCount.TryAdd(movieFanart.MovieId, 1)) fanartCount[movieFanart.MovieId] += 1;
            }
        }

        // AniDB Characters
        if (settings.AniDb.DownloadCharacters)
        {
            foreach (var chr in RepoFactory.AniDB_Character.GetAll())
            {
                if (string.IsNullOrEmpty(chr.GetPosterPath())) continue;
                var fileExists = File.Exists(chr.GetPosterPath());
                if (fileExists) continue;
                var AnimeID = RepoFactory.AniDB_Anime_Character.GetByCharID(chr.CharID)?.FirstOrDefault()?.AnimeID ?? 0;
                if (AnimeID == 0) continue;

                await scheduler.StartJob<GetAniDBImagesJob>(c => c.AnimeID = AnimeID);
            }
        }

        // AniDB Creators
        if (settings.AniDb.DownloadCreators)
        {
            foreach (var seiyuu in RepoFactory.AniDB_Seiyuu.GetAll())
            {
                if (string.IsNullOrEmpty(seiyuu.GetPosterPath())) continue;
                var fileExists = File.Exists(seiyuu.GetPosterPath());
                if (fileExists) continue;
                var chr = RepoFactory.AniDB_Character_Seiyuu.GetBySeiyuuID(seiyuu.SeiyuuID).FirstOrDefault();
                if (chr == null) continue;

                var AnimeID = RepoFactory.AniDB_Anime_Character.GetByCharID(chr.CharID)?.FirstOrDefault()?.AnimeID ?? 0;
                if (AnimeID == 0) continue;

                await scheduler.StartJob<GetAniDBImagesJob>(c => c.AnimeID = AnimeID);
            }
        }
    }

    public async Task RunImport_ScanTvDB()
    {
        await _tvdbHelper.ScanForMatches();
    }

    public void RunImport_ScanTrakt()
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken)) return;
        _traktHelper.ScanForMatches();
    }

    public async Task RunImport_ScanMovieDB()
    {
        await _movieDBHelper.ScanForMatches();
    }

    public async Task RunImport_UpdateTvDB(bool forced)
    {
        await _tvdbHelper.UpdateAllInfo(forced);
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
        using var session = DatabaseFactory.SessionFactory.OpenSession();

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
                        _logger.LogInformation("RemoveRecordsWithOrphanedImportFolder : {Filename}", v.FileName);
                        seriesToUpdate.UnionWith(v.GetAnimeEpisodes().Select(a => a.GetAnimeSeries())
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
                places = v.Places?.Except(places).ToList() ?? new List<SVR_VideoLocal_Place>();
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
            _logger.LogInformation("RemoveOrphanedVideoLocal : {Filename}", v.FileName);
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

        _logger.LogInformation("Remove Missing Files: Finished");
    }

    public async Task<string> DeleteImportFolder(int importFolderID, bool removeFromMyList = true)
    {
        try
        {
            var affectedSeries = new HashSet<SVR_AnimeSeries>();
            var vids = RepoFactory.VideoLocalPlace.GetByImportFolder(importFolderID);
            _logger.LogInformation("Deleting {VidsCount} video local records", vids.Count);
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            foreach (var vid in vids)
            {
                await _placeService.RemoveRecordWithOpenTransaction(session, vid, affectedSeries, removeFromMyList);
            }

            // delete the import folder
            RepoFactory.ImportFolder.Delete(importFolderID);

            foreach (var ser in affectedSeries)
                ser.QueueUpdateStats();

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return ex.Message;
        }
    }

    public void UpdateAllStats()
    {
        foreach (var ser in RepoFactory.AnimeSeries.GetAll())
        {
            ser.QueueUpdateStats();
        }
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

    public async Task CheckForTvDBUpdates(bool forceRefresh)
    {
        var settings = _settingsProvider.GetSettings();
        if (settings.TvDB.UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;

        var scheduler = await _schedulerFactory.GetScheduler();
        var freqHours = Utils.GetScheduledHours(settings.TvDB.UpdateFrequency);

        // update tvdb info every 12 hours

        var sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TvDBInfo);
        if (sched != null)
        {
            // if we have run this in the last 12 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - sched.LastUpdate;
            if (tsLastRun.TotalHours < freqHours && !forceRefresh) return;
        }

        var tvDBIDs = new List<int>();
        var tvDBOnline = false;
        var serverTime = _tvdbHelper.IncrementalTvDBUpdate(ref tvDBIDs, ref tvDBOnline);

        if (tvDBOnline)
        {
            foreach (var tvid in tvDBIDs)
            {
                // download and update series info, episode info and episode images
                // will also download fanart, posters and wide banners
                await scheduler.StartJob<GetTvDBSeriesJob>(c =>
                    {
                        c.TvDBSeriesID = tvid;
                        c.ForceRefresh = true;
                    }
                );
            }
        }

        sched ??= new ScheduledUpdate { UpdateType = (int)ScheduledUpdateType.TvDBInfo };

        sched.LastUpdate = DateTime.Now;
        sched.UpdateDetails = serverTime;
        RepoFactory.ScheduledUpdate.Save(sched);

        await _tvdbHelper.ScanForMatches();
    }

    public async Task CheckForUnreadNotifications(bool forceRefresh)
    {
        var sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBNotify);
        if (sched == null)
        {
            sched = new()
            {
                UpdateType = (int)ScheduledUpdateType.AniDBNotify,
                UpdateDetails = string.Empty
            };
        }
        else
        {
            var settings = _settingsProvider.GetSettings();
            var freqHours = Utils.GetScheduledHours(settings.AniDb.Notification_UpdateFrequency);

            // if we have run this in the last freqHours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - sched.LastUpdate;
            if (!forceRefresh && tsLastRun.TotalHours < freqHours) return;
        }

        sched.LastUpdate = DateTime.Now;
        RepoFactory.ScheduledUpdate.Save(sched);

        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<GetAniDBNotifyJob>(n => n.ForceRefresh = forceRefresh);
        // automatically handle moved files after fetching notifications
        await HandleMovedFiles(false);
    }

    public async Task HandleMovedFiles(bool force)
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
                    await scheduler.StartJob<ProcessFileMovedMessageJob>(c => c.Message = msg);
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

        var sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBCalendar);
        if (sched != null)
        {
            // if we have run this in the last 12 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - sched.LastUpdate;
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

        var sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBUpdates);
        if (sched != null)
        {
            // if we have run this in the last 12 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - sched.LastUpdate;
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

        var sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBMyListSync);
        if (sched != null)
        {
            // if we have run this in the last 24 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - sched.LastUpdate;
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
        var sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TraktUpdate);
        if (sched == null)
        {
            sched = new ScheduledUpdate
            {
                UpdateType = (int)ScheduledUpdateType.TraktUpdate, UpdateDetails = string.Empty
            };
        }
        else
        {
            var freqHours = Utils.GetScheduledHours(settings.TraktTv.UpdateFrequency);

            // if we have run this in the last xxx hours then exit
            var tsLastRun = DateTime.Now - sched.LastUpdate;
            if (tsLastRun.TotalHours < freqHours && !forceRefresh) return;
        }

        sched.LastUpdate = DateTime.Now;
        RepoFactory.ScheduledUpdate.Save(sched);

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
                var sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TraktToken)
                            ?? new ScheduledUpdate { UpdateType = (int)ScheduledUpdateType.TraktToken, UpdateDetails = string.Empty };

                sched.LastUpdate = DateTime.Now;
                RepoFactory.ScheduledUpdate.Save(sched);

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

            await scheduler.StartJob<ProcessFileJob>(c =>
                {
                    c.VideoLocalID = vl.VideoLocalID;
                    c.ForceAniDB = true;
                }
            );
        }

        // now check for any files which have been manually linked and are less than 30 days old


        sched ??= new ScheduledUpdate
        {
            UpdateType = (int)ScheduledUpdateType.AniDBFileUpdates, UpdateDetails = string.Empty
        };

        sched.LastUpdate = DateTime.Now;
        RepoFactory.ScheduledUpdate.Save(sched);
    }

    public  void CheckForPreviouslyIgnored()
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
            _logger.LogError(ex, "Error in CheckForPreviouslyIgnored: {Ex}", ex);
        }
    }
}
