using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using NHibernate;
using Quartz;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Events;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Services;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video;
using Shoko.Server.Databases;
using Shoko.Server.MediaInfo;
using Shoko.Server.MediaInfo.Subtitles;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Providers.AniDB.Release;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Services.Ogg;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#pragma warning disable CS0618
#nullable enable
namespace Shoko.Server.Services;

public class VideoService : IVideoService
{

    private readonly ILogger<VideoService> _logger;

    private readonly VideoLocal_PlaceRepository _placeRepository;

    private readonly VideoLocalRepository _videoLocalRepository;

    private readonly VideoLocal_PlaceRepository _videoLocalPlaceRepository;

    private readonly VideoLocal_HashDigestRepository _videoLocalHashRepository;

    private readonly ShokoManagedFolderRepository _managedFolderRepository;

    private readonly CrossRef_File_EpisodeRepository _crossRefRepository;

    private readonly FileNameHashRepository _fileNameHashRepository;

    private readonly AniDB_EpisodeRepository _anidbEpisodeRepository;

    private readonly StoredReleaseInfoRepository _storedReleaseInfoRepository;

    private readonly VideoHashingService _videoHashingService;

    private readonly IVideoReleaseService _videoReleaseService;

    private readonly IRelocationService _relocationService;

    private readonly ISchedulerFactory _schedulerFactory;

    private readonly ISettingsProvider _settingsProvider;

    private readonly DatabaseFactory _databaseFactory;

    /// <inheritdoc/>
    public event EventHandler<FileDetectedEventArgs>? VideoFileDetected;

    /// <inheritdoc/>
    public event EventHandler<FileEventArgs>? VideoFileDeleted;

    /// <inheritdoc/>
    public event EventHandler<FileHashedEventArgs>? VideoFileHashed;

    /// <inheritdoc/>
    public event EventHandler<FileRelocatedEventArgs>? VideoFileRelocated;

    /// <inheritdoc/>
    public event EventHandler<ManagedFolderChangedEventArgs>? ManagedFolderAdded;

    /// <inheritdoc/>
    public event EventHandler<ManagedFolderChangedEventArgs>? ManagedFolderUpdated;

    /// <inheritdoc/>
    public event EventHandler<ManagedFolderChangedEventArgs>? ManagedFolderRemoved;

    public VideoService(
        ILogger<VideoService> logger,
        VideoLocal_PlaceRepository placeRepository,
        VideoLocalRepository videoLocalRepository,
        VideoLocal_PlaceRepository videoLocalPlaceRepository,
        VideoLocal_HashDigestRepository videoLocalHashRepository,
        ShokoManagedFolderRepository managedFolderRepository,
        CrossRef_File_EpisodeRepository crossRefRepository,
        FileNameHashRepository fileNameHashRepository,
        AniDB_EpisodeRepository anidbEpisodeRepository,
        StoredReleaseInfoRepository storedReleaseInfoRepository,
        IVideoHashingService videoHashingService,
        IVideoReleaseService videoReleaseService,
        IRelocationService relocationService,
        ISchedulerFactory schedulerFactory,
        ISettingsProvider settingsProvider,
        DatabaseFactory databaseFactory
    )
    {
        _logger = logger;
        _placeRepository = placeRepository;
        _videoLocalRepository = videoLocalRepository;
        _videoLocalPlaceRepository = videoLocalPlaceRepository;
        _videoLocalHashRepository = videoLocalHashRepository;
        _managedFolderRepository = managedFolderRepository;
        _crossRefRepository = crossRefRepository;
        _fileNameHashRepository = fileNameHashRepository;
        _anidbEpisodeRepository = anidbEpisodeRepository;
        _storedReleaseInfoRepository = storedReleaseInfoRepository;
        _videoHashingService = (VideoHashingService)videoHashingService;
        _videoReleaseService = videoReleaseService;
        _relocationService = relocationService;
        _schedulerFactory = schedulerFactory;
        _settingsProvider = settingsProvider;
        _databaseFactory = databaseFactory;

        ShokoEventHandler.Instance.FileDeleted += OnFileDeleted;
        _videoHashingService.FileHashed += OnFileHashed;
        _relocationService.FileRelocated += OnFileRelocated;
        _managedFolderRepository.ManagedFolderAdded += OnManagedFolderAdded;
        _managedFolderRepository.ManagedFolderUpdated += OnManagedFolderUpdated;
        _managedFolderRepository.ManagedFolderRemoved += OnManagedFolderRemoved;
    }

    ~VideoService()
    {
        ShokoEventHandler.Instance.FileDeleted -= OnFileDeleted;
        _videoHashingService.FileHashed -= OnFileHashed;
        _relocationService.FileRelocated -= OnFileRelocated;
        _managedFolderRepository.ManagedFolderAdded -= OnManagedFolderAdded;
        _managedFolderRepository.ManagedFolderUpdated -= OnManagedFolderUpdated;
        _managedFolderRepository.ManagedFolderRemoved -= OnManagedFolderRemoved;
    }

    #region Event Forwarding

    private void OnFileDeleted(object? sender, FileEventArgs eventArgs)
    {
        VideoFileDeleted?.Invoke(this, eventArgs);
    }

    private void OnFileHashed(object? sender, FileHashedEventArgs eventArgs)
    {
        VideoFileHashed?.Invoke(this, eventArgs);
    }

    private void OnFileRelocated(object? sender, FileRelocatedEventArgs eventArgs)
    {
        VideoFileRelocated?.Invoke(this, eventArgs);
    }

    private void OnManagedFolderAdded(object? sender, ManagedFolderChangedEventArgs eventArgs)
    {
        ManagedFolderAdded?.Invoke(this, eventArgs);
    }

    private void OnManagedFolderUpdated(object? sender, ManagedFolderChangedEventArgs eventArgs)
    {
        ManagedFolderUpdated?.Invoke(this, eventArgs);
    }

    private void OnManagedFolderRemoved(object? sender, ManagedFolderChangedEventArgs eventArgs)
    {
        ManagedFolderRemoved?.Invoke(this, eventArgs);
    }

    #endregion Event Forwarding

    #region Setup

    private IReadOnlyList<IManagedFolderIgnoreRule> _ignoreRules = [];

    public IReadOnlyList<IManagedFolderIgnoreRule> IgnoreRules => _ignoreRules;

    public void AddParts(IEnumerable<IManagedFolderIgnoreRule> rules)
    {
        if (_ignoreRules.Count > 0)
            return;

        _ignoreRules = rules.ToList();
    }

    #endregion

    #region Video File

    /// <inheritdoc/>
    public IEnumerable<IVideoFile> GetAllVideoFiles()
        => _placeRepository.GetAll().AsQueryable();

    /// <inheritdoc/>
    public IVideoFile? GetVideoFileByID(int fileID)
        => fileID <= 0 ? null : _placeRepository.GetByID(fileID);

    // This will be slow for now, but at least it gets the job done.
    /// <inheritdoc/>
    public IVideoFile? GetVideoFileByAbsolutePath(string absolutePath)
        => string.IsNullOrWhiteSpace(absolutePath) ? null : _placeRepository.GetAll()
            .FirstOrDefault(a => string.Equals(a.Path, absolutePath, PlatformUtility.StringComparison));

    /// <inheritdoc/>
    public IReadOnlyList<IVideoFile> GetVideoFilesByAbsolutePath(string absolutePath)
        => string.IsNullOrWhiteSpace(absolutePath) ? [] : _placeRepository.GetAll()
            .Where(a => string.Equals(a.Path, absolutePath, PlatformUtility.StringComparison))
            .ToList();

    /// <inheritdoc/>
    public IVideoFile? GetVideoFileByRelativePath(string relativePath, IManagedFolder? managedFolder = null)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        if (managedFolder is null)
            return _placeRepository.GetByRelativePath(relativePath);

        return _placeRepository.GetByRelativePathAndManagedFolderID(relativePath, managedFolder.ID);
    }

    /// <inheritdoc/>
    public IReadOnlyList<IVideoFile> GetVideoFilesInManagedFolder(IManagedFolder managedFolder, string? relativePath = null)
    {
        if (managedFolder is not ShokoManagedFolder folder)
            return [];

        relativePath = PlatformUtility.NormalizePath(relativePath, stripLeadingSlash: true);
        var videoFiles = _placeRepository.GetByManagedFolderID(folder.ID);
        if (string.IsNullOrEmpty(relativePath))
            return videoFiles;

        return videoFiles
            .Where(a => string.IsNullOrEmpty(relativePath) || a.RelativePath.StartsWith(relativePath, PlatformUtility.StringComparison))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task NotifyVideoFileChangeDetected(string absolutePath, bool updateMylist = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(absolutePath);

        var (managedFolder, relativePath) = _managedFolderRepository.GetFromAbsolutePath(absolutePath);
        if (managedFolder is null)
            throw new InvalidOperationException($"The path is outside of any managed folders: {absolutePath}");

        await NotifyVideoFileChangeDetected(managedFolder, relativePath, updateMylist);
    }

    /// <inheritdoc/>
    public async Task NotifyVideoFileChangeDetected(IManagedFolder managedFolder, string? relativePath = null, bool updateMylist = true)
    {
        ArgumentNullException.ThrowIfNull(managedFolder);

        // Don't trust the input to be cleaned beforehand.
        relativePath = PlatformUtility.NormalizePath(relativePath, stripLeadingSlash: true);

        // If some plugin or RESTful client decide to (ab)use this method to scan a directory,
        // then forward it to the scan method instead.
        var absolutePath = string.IsNullOrEmpty(relativePath)
             ? managedFolder.Path
             : Path.Join(managedFolder.Path, relativePath);
        if (string.IsNullOrEmpty(relativePath) || Directory.Exists(absolutePath))
        {
            await ScanManagedFolder(managedFolder, relativePath: relativePath, onlyNewFiles: false, skipMylist: !updateMylist, cleanUpStructure: false);
            return;
        }

        if (!TryGetVideoAndLocation(managedFolder, relativePath, out var video, out var videoLocation))
        {
            // Logging occurs in TryGetVideoAndLocation.
            return;
        }

        var locationAvailable = videoLocation.IsAvailable;
        if (!locationAvailable && videoLocation.ID is 0)
        {
            _logger.LogInformation("File is unavailable, skipping: {Path}", absolutePath);
            return;
        }

        var wasDeleted = await _videoHashingService.DeduplicateAndCleanup(video, videoLocation, updateMylist, locationAvailable);
        if (wasDeleted)
        {
            _logger.LogInformation("File was deleted during missing file cleanup and/or file auto de-duplication: {Path}", absolutePath);
            return;
        }

        var settings = _settingsProvider.GetSettings();
        var videoIsKnown = !string.IsNullOrEmpty(video.Hash) && video.FileSize > 0;
        var hasXrefs = videoIsKnown && video.EpisodeCrossReferences is { Count: > 0 };
        var shouldSave = videoIsKnown && locationAvailable && videoLocation.ID is 0;
        var shouldHash = !videoIsKnown || (video.Hashes is { } hashes && (hashes.Count == 0 || _videoHashingService.AllEnabledHashTypes.Any(a => !hashes.Any(b => b.Type == a))));
        var shouldRelocate = hasXrefs && !shouldHash && locationAvailable && settings.Plugins.Renamer.RelocateOnImport && (
            managedFolder.DropFolderType.HasFlag(DropFolderType.Source) ||
            (managedFolder.DropFolderType.HasFlag(DropFolderType.Destination) && settings.Plugins.Renamer.AllowRelocationInsideDestinationOnImport)
        );
        if (locationAvailable)
        {
            if (videoLocation.ID is 0)
            {
                try
                {
                    VideoFileDetected?.Invoke(null, new(relativePath, new(absolutePath), managedFolder));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Got an error in a VideoFileDetected event.");
                }
            }

            if (hasXrefs && !video.DateTimeImported.HasValue)
            {
                video.DateTimeImported = DateTime.Now;
                shouldSave = true;
            }
        }

        if (shouldSave)
        {
            _logger.LogTrace("Saving video record for path: {Path} (Hash={Hash},Size={Size})", absolutePath, video.Hash, video.FileSize);
            _videoLocalRepository.Save(video, true);

            _logger.LogTrace("Saving video file record for path: {Path} (Hash={Hash},Size={Size})", absolutePath, video.Hash, video.FileSize);
            videoLocation.VideoID = video.VideoLocalID;
            _videoLocalPlaceRepository.Save(videoLocation);
        }

        if (shouldHash)
        {
            _logger.LogTrace("Scheduling video hashing for: {Path}", absolutePath);
            try
            {
                await _videoHashingService.ScheduleGetHashesForPath(absolutePath, skipMylist: !updateMylist);
            }
            catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException)
            {
                _logger.LogWarning(ex, "{Message}", ex.Message);
            }
            return;
        }

        if (shouldRelocate)
        {
            _logger.LogTrace("Scheduling video relocation for: {Path}", absolutePath);
            var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);
            await scheduler.StartJob<RenameMoveFileLocationJob>(b => (b.ManagedFolderID, b.RelativePath) = (managedFolder.ID, relativePath));
        }

        if (hasXrefs)
        {
            _logger.LogTrace("Found existing video file with hashes and release info: {Path} (ED2K={Hash})", absolutePath, video.Hash);
            return;
        }

        if (!_videoReleaseService.AutoMatchEnabled)
        {
            _logger.LogTrace("Found existing video file with hashes but without release info and auto-match is disabled: {Path} (ED2K={Hash})", absolutePath, video.Hash);
            return;
        }

        _logger.LogTrace("Found existing video file with hashes but without release info: {Path} (ED2K={Hash})", absolutePath, video.Hash);
        await _videoReleaseService.ScheduleFindReleaseForVideo(video, addToMylist: updateMylist);
    }

    private bool TryGetVideoAndLocation(IManagedFolder managedFolder, string relativePath, [NotNullWhen(true)] out VideoLocal? video, [NotNullWhen(true)] out VideoLocal_Place? videoLocation)
    {
        // check if we have already processed this file
        video = null;
        videoLocation = _videoLocalPlaceRepository.GetByRelativePathAndManagedFolderID(relativePath, managedFolder.ID);
        if (videoLocation is not null)
        {
            video = videoLocation.VideoLocal;
            if (video is not null)
            {
                _logger.LogTrace("Video record found in database: {Filename}", relativePath);

                // This will only happen with DB corruption, so just clean up the mess.
                if (videoLocation.Path is not { Length: > 0 })
                {
                    _logger.LogTrace("Video file path is non-existent, removing record.");
                    if (video.Places.Count == 1)
                    {
                        _videoLocalRepository.Delete(video);
                        video = null;
                    }

                    _videoLocalPlaceRepository.Delete(videoLocation);
                    videoLocation = null;
                }
            }
        }

        if (video is null)
        {
            _logger.LogTrace("No existing video record, creating temporary record");
            video = new VideoLocal
            {
                DateTimeUpdated = DateTime.Now,
                DateTimeCreated = DateTime.Now,
                FileName = Path.GetFileName(relativePath),
                Hash = string.Empty,
            };
        }

        if (videoLocation is null)
        {
            if (!Utils.IsVideo(relativePath))
            {
                _logger.LogInformation("File does not have an acceptable video extension, skipping: {Path}", relativePath);
                return false;
            }

            // If this is a new record, check if it's a symlink of another known
            // file or if it's a previously known file.
            if (video is { VideoLocalID: 0 })
            {
                var originalPath = PlatformUtility.EnsureUsablePath(Path.Join(managedFolder.Path, relativePath));
                var resolvedPath = File.ResolveLinkTarget(originalPath, true)?.FullName;
                if (string.IsNullOrEmpty(resolvedPath))
                {
                    resolvedPath = originalPath;
                }
                else
                {
                    _logger.LogTrace("File is a symbolic link. Original path: {OriginalFilePath}, Resolved path: {ResolvedFilePath}", originalPath, resolvedPath);
                    if (
                        _managedFolderRepository.GetFromAbsolutePath(resolvedPath) is ({ Path.Length: > 0 } resolvedFolder, { Length: > 0 } resolvedRelativePath) &&
                        _videoLocalPlaceRepository.GetByRelativePathAndManagedFolderID(resolvedRelativePath, resolvedFolder.ID) is { } resolvedLocation &&
                        resolvedLocation.VideoLocal is { } resolvedVideo
                    )
                    {
                        _logger.LogTrace("Found video for symbolic link: {ResolvedPath} (Hash={Hash},Size={Size})", resolvedPath, resolvedVideo.Hash, resolvedVideo.FileSize);
                        video = resolvedVideo;
                    }
                }

                // Check again in case we resolved a symlink above.
                if (video is { VideoLocalID: 0 })
                {
                    if (!_videoHashingService.TryGetFileSize(originalPath, resolvedPath, out var fileSize, out var e))
                    {
                        _logger.LogWarning(e, "Could not access file to read file size or file size is 0: {ResolvedPath}", originalPath);
                        return false;
                    }

                    var fileName = Path.GetFileName(resolvedPath);
                    if (TryGetVideoFromCrossReferenceTable(fileName, fileSize, out var otherVideo))
                    {
                        _logger.LogTrace("Found video for file name and size through CrossRef_File_Episode table: {ResolvedPath} (Hash={Hash},Size={Size})", resolvedPath, otherVideo.Hash, fileSize);
                        video = otherVideo;
                    }
                    else if (TryGetVideoFromFileNameHashTable(fileName, fileSize, out otherVideo))
                    {
                        _logger.LogTrace("Found video for file name and size through FileNameHash table: {ResolvedPath} (Hash={Hash},Size={Size})", resolvedPath, otherVideo.Hash, fileSize);
                        video = otherVideo;
                    }
                }
            }

            _logger.LogTrace("No existing video file record, creating a new record");
            videoLocation = new VideoLocal_Place
            {
                RelativePath = relativePath,
                ManagedFolderID = managedFolder.ID,
            };
            if (video.VideoLocalID != 0) videoLocation.VideoID = video.VideoLocalID;
        }

        return true;
    }

    private bool TryGetVideoFromCrossReferenceTable(string filename, long fileSize, [NotNullWhen(true)] out VideoLocal? video)
    {
        var xrefs = _crossRefRepository.GetByFileNameAndSize(filename, fileSize);
        if (xrefs.Count == 0)
        {
            video = null;
            return false;
        }

        video = _videoLocalRepository.GetByEd2kAndSize(xrefs[0].Hash, fileSize);
        return video is not null;
    }

    private bool TryGetVideoFromFileNameHashTable(string filename, long fileSize, [NotNullWhen(true)] out VideoLocal? video)
    {
        // If we have more than one record it probably means there is some sort
        // of corruption. Let's delete all local records.
        var hashes = _fileNameHashRepository.GetByFileNameAndSize(filename, fileSize);
        if (hashes.Count > 1)
        {
            _fileNameHashRepository.Delete(hashes);
            hashes = _fileNameHashRepository.GetByFileNameAndSize(filename, fileSize);
        }

        if (hashes is not { Count: 1 })
        {
            video = null;
            return false;
        }

        video = _videoLocalRepository.GetByEd2kAndSize(hashes[0].Hash, fileSize);
        return video is not null;
    }

    public async Task DeleteVideoFile(IVideoFile videoFile, bool removeFile = true, bool removeFolders = true)
    {
        if (videoFile is not VideoLocal_Place place)
            return;

        if (removeFile)
            await RemoveRecordAndDeletePhysicalFile(place, removeFolders);
        else
            await RemoveRecord(place);
    }

    public async Task DeleteVideoFiles(IEnumerable<IVideoFile> videoFiles, bool removeFiles = true, bool removeFolders = true)
    {
        foreach (var videoFile in videoFiles)
        {
            await DeleteVideoFile(videoFile, removeFiles, removeFolders);
        }
    }

    #region Video File | Delete

    public async Task RemoveRecordAndDeletePhysicalFile(VideoLocal_Place place, bool deleteFolder = true, bool updateMyList = true)
    {
        _logger.LogInformation("Deleting video local place record and file: {Place}", place.Path ?? place.ID.ToString());

        if (!File.Exists(place.Path))
        {
            _logger.LogInformation("Unable to find file. Removing Record: {Place}", place.Path ?? place.RelativePath);
            await RemoveRecord(place, updateMyList);
            return;
        }

        try
        {
            File.Delete(place.Path);
            DeleteExternalSubtitles(place.Path);
        }
        catch (FileNotFoundException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to delete file \'{Place}\': {Ex}", place.Path, ex);
            throw;
        }

        if (deleteFolder)
            RecursiveDeleteEmptyDirectories(Path.GetDirectoryName(place.Path), place.ManagedFolder!.Path);

        await RemoveRecord(place, updateMyList);
    }

    public async Task RemoveAndDeleteFileWithOpenTransaction(ISession session, VideoLocal_Place place, HashSet<AnimeSeries> seriesToUpdate, bool deleteFolders = true, bool updateMyList = true)
    {
        try
        {
            _logger.LogInformation("Deleting video local place record and file: {Place}", place.Path ?? place.ID.ToString());

            if (!File.Exists(place.Path))
            {
                _logger.LogInformation("Unable to find file. Removing Record: {FullServerPath}", place.Path);
                await RemoveRecordWithOpenTransaction(session, place, seriesToUpdate, updateMyList);
                return;
            }

            try
            {
                File.Delete(place.Path);
                DeleteExternalSubtitles(place.Path);
            }
            catch (FileNotFoundException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to delete file \'{Place}\': {Ex}", place.Path, ex);
                return;
            }

            if (deleteFolders)
                RecursiveDeleteEmptyDirectories(Path.GetDirectoryName(place.Path), place.ManagedFolder!.Path);

            await RemoveRecordWithOpenTransaction(session, place, seriesToUpdate, updateMyList);
            // For deletion of files from Trakt, we will rely on the Daily sync
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not delete file and remove record for \"{Place}\": {Ex}", place.Path ?? place.ID.ToString(),
                ex);
        }
    }

    private void DeleteExternalSubtitles(string originalFileName)
    {
        try
        {
            var textStreams = SubtitleHelper.GetSubtitleStreams(originalFileName);
            // move any subtitle files
            foreach (var subtitleFile in textStreams)
            {
                if (string.IsNullOrEmpty(subtitleFile.Filename)) continue;

                var srcParent = Path.GetDirectoryName(originalFileName);
                if (string.IsNullOrEmpty(srcParent)) continue;

                var subPath = Path.Combine(srcParent, subtitleFile.Filename);
                if (!File.Exists(subPath)) continue;

                try
                {
                    File.Delete(subPath);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unable to delete file: \"{SubtitleFile}\"", subtitleFile.Filename);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an error deleting external subtitles: {Ex}", ex);
        }
    }

    public async Task RemoveRecord(VideoLocal_Place place, bool updateMyListStatus = true)
    {
        _logger.LogInformation("Removing VideoLocal_Place record for: {Place}", place.Path ?? place.ID.ToString());
        var seriesToUpdate = new List<AnimeSeries>();
        var v = place.VideoLocal;
        var scheduler = await _schedulerFactory.GetScheduler();

        using (var session = _databaseFactory.SessionFactory.OpenSession())
        {
            if (v?.Places?.Count <= 1)
            {
                if (updateMyListStatus)
                    await ScheduleRemovalFromMyList(v);

                try
                {
                    ShokoEventHandler.Instance.OnFileDeleted(place.ManagedFolder!, place, v);
                }
                catch
                {
                    // ignore
                }

                BaseRepository.Lock(session, s =>
                {
                    using var transaction = s.BeginTransaction();
                    _videoLocalPlaceRepository.DeleteWithOpenTransaction(s, place);

                    seriesToUpdate.AddRange(
                        v
                            .AnimeEpisodes
                            .DistinctBy(a => a.AnimeSeriesID)
                            .Select(a => a.AnimeSeries)
                            .WhereNotNull()
                    );
                    _videoLocalRepository.DeleteWithOpenTransaction(s, v);
                    transaction.Commit();
                });
            }
            else
            {
                if (v is not null)
                {
                    try
                    {
                        ShokoEventHandler.Instance.OnFileDeleted(place.ManagedFolder!, place, v);
                    }
                    catch
                    {
                        // ignore
                    }
                }

                BaseRepository.Lock(session, s =>
                {
                    using var transaction = s.BeginTransaction();
                    _videoLocalPlaceRepository.DeleteWithOpenTransaction(s, place);
                    transaction.Commit();
                });
            }
        }

        await Task.WhenAll(seriesToUpdate.Select(a => scheduler.StartJob<RefreshAnimeStatsJob>(b => b.AnimeID = a.AniDB_ID)));
    }

    public async Task RemoveRecordWithOpenTransaction(ISession session, VideoLocal_Place place, ICollection<AnimeSeries> seriesToUpdate,
        bool updateMyListStatus = true)
    {
        _logger.LogInformation("Removing VideoLocal_Place record for: {Place}", place.Path ?? place.ID.ToString());
        var v = place.VideoLocal;

        if (v?.Places?.Count <= 1)
        {
            if (updateMyListStatus)
                await ScheduleRemovalFromMyList(v);

            var eps = v.AnimeEpisodes?.WhereNotNull().ToList();
            eps?.DistinctBy(a => a.AnimeSeriesID).Select(a => a.AnimeSeries).WhereNotNull().ToList().ForEach(seriesToUpdate.Add);

            try
            {
                ShokoEventHandler.Instance.OnFileDeleted(place.ManagedFolder!, place, v);
            }
            catch
            {
                // ignore
            }

            BaseRepository.Lock(() =>
            {
                using var transaction = session.BeginTransaction();
                _videoLocalPlaceRepository.DeleteWithOpenTransaction(session, place);
                _videoLocalRepository.DeleteWithOpenTransaction(session, v);
                transaction.Commit();
            });
        }
        else
        {
            if (v is not null)
            {
                try
                {
                    ShokoEventHandler.Instance.OnFileDeleted(place.ManagedFolder!, place, v);
                }
                catch
                {
                    // ignore
                }
            }

            BaseRepository.Lock(() =>
            {
                using var transaction = session.BeginTransaction();
                _videoLocalPlaceRepository.DeleteWithOpenTransaction(session, place);
                transaction.Commit();
            });
        }
    }

    public async Task ScheduleRemovalFromMyList(VideoLocal video)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        if (_storedReleaseInfoRepository.GetByEd2kAndFileSize(video.Hash, video.FileSize) is { ReleaseURI: not null } releaseInfo && releaseInfo.ReleaseURI.StartsWith(AnidbReleaseProvider.ReleasePrefix))
        {
            await scheduler.StartJob<DeleteFileFromMyListJob>(c =>
                {
                    c.Hash = video.Hash;
                    c.FileSize = video.FileSize;
                }
            );
        }
        else
        {
            var xrefs = video.EpisodeCrossReferences;
            foreach (var xref in xrefs)
            {
                if (xref.AnimeID is 0)
                    continue;

                var ep = _anidbEpisodeRepository.GetByEpisodeID(xref.EpisodeID);
                if (ep is null)
                    continue;

                await scheduler.StartJob<DeleteFileFromMyListJob>(c =>
                    {
                        c.AnimeID = xref.AnimeID;
                        c.EpisodeType = ep.EpisodeType;
                        c.EpisodeNumber = ep.EpisodeNumber;
                    }
                );
            }
        }
    }

    #endregion

    #endregion Video File

    #region Video

    /// <inheritdoc/>
    public IEnumerable<IVideo> GetAllVideos()
        => _videoLocalRepository.GetAll().AsQueryable();

    /// <inheritdoc/>
    public IVideo? GetVideoByID(int videoID)
        => videoID <= 0 ? null : _videoLocalRepository.GetByID(videoID);

    /// <inheritdoc/>
    public IVideo? GetVideoByHash(string hash, string algorithm = "ED2K")
        => GetAllVideoByHash(hash, algorithm) is { Count: 1 } videos ? videos[0] : null;

    /// <inheritdoc/>
    public IVideo? GetVideoByHashAndSize(string hash, long fileSize, string algorithm = "ED2K")
        => GetAllVideoByHash(hash, algorithm).Where(a => a.Size == fileSize).ToList() is { Count: 1 } videos ? videos[0] : null;

    /// <inheritdoc/>
    public IReadOnlyList<IVideo> GetAllVideoByHash(string hash, string algorithm = "ED2K")
        => _videoLocalHashRepository.GetByHashTypeAndValue(algorithm, hash)
            .Select(a => _videoLocalRepository.GetByID(a.VideoLocalID))
            .WhereNotNull()
            .ToList();

    public IReadOnlyList<IVideo> GetAllVideoByHash(string hash, string algorithm, string? metadata)
        => _videoLocalHashRepository.GetByHashTypeAndValue(algorithm, hash)
            .Where(a => string.Equals(a.Metadata, metadata, StringComparison.Ordinal))
            .Select(a => _videoLocalRepository.GetByID(a.VideoLocalID))
            .WhereNotNull()
            .ToList();

    public async Task DeleteVideo(IVideo video, bool removeFiles = true, bool removeFolders = true)
    {
        foreach (var videoFile in video.Files)
        {
            await DeleteVideoFile(videoFile, removeFiles, removeFolders);
        }
    }

    public async Task DeleteVideos(IEnumerable<IVideo> videos, bool removeFiles = true, bool removeFolders = true)
    {
        foreach (var video in videos)
        {
            foreach (var videoFile in video.Files)
            {
                await DeleteVideoFile(videoFile, removeFiles, removeFolders);
            }
        }

    }

    #endregion Video

    #region Managed Folder

    /// <inheritdoc/>
    public IEnumerable<IManagedFolder> GetAllManagedFolders()
        => _managedFolderRepository.GetAll();

    /// <inheritdoc/>
    public IManagedFolder? GetManagedFolderByID(int folderID)
        => folderID <= 0 ? null : _managedFolderRepository.GetByID(folderID);

    public IManagedFolder? GetManagedFolderByPath(string path)
         => string.IsNullOrWhiteSpace(path) ? null : _managedFolderRepository.GetByImportLocation(path);

    public IManagedFolder AddManagedFolder(string name, string path, DropFolderType dropFolderType = DropFolderType.Excluded, bool watchForNewFiles = false)
        => _managedFolderRepository.SaveFolder(new ShokoManagedFolder()
        {
            Name = name,
            Path = path,
            DropFolderType = dropFolderType,
            IsWatched = watchForNewFiles,
        });

    public IManagedFolder UpdateManagedFolder(IManagedFolder folder, string? name = null, string? path = null, DropFolderType? dropFolderType = null, bool? watchForNewFiles = null)
    {
        var managedFolder = (ShokoManagedFolder)folder;
        if (name is { Length: > 0 }) managedFolder.Name = name;
        if (path is { Length: > 0 }) managedFolder.Path = path;
        if (dropFolderType is not null) managedFolder.DropFolderType = dropFolderType.Value;
        if (watchForNewFiles is not null) managedFolder.IsWatched = watchForNewFiles.Value;
        return _managedFolderRepository.SaveFolder(managedFolder);
    }

    public async Task RemoveManagedFolder(IManagedFolder folder, bool keepRecords = false, bool removeMyList = true)
    {
        if (keepRecords)
        {
            _managedFolderRepository.Delete(folder.ID);
            return;
        }

        var videos = _videoLocalPlaceRepository.GetByManagedFolderID(folder.ID);
        _logger.LogInformation("Deleting {VidsCount} video local records", videos.Count);

        var affectedSeries = new HashSet<AnimeSeries>();
        using var session = _databaseFactory.SessionFactory.OpenSession();
        foreach (var vid in videos)
            await RemoveRecordWithOpenTransaction(session, vid, affectedSeries, removeMyList);

        _managedFolderRepository.Delete(folder.ID);

        var scheduler = await _schedulerFactory.GetScheduler();
        await Task.WhenAll(affectedSeries.Select(a => scheduler.StartJob<RefreshAnimeStatsJob>(b => b.AnimeID = a.AniDB_ID)));
    }

    public async Task ScanManagedFolder(IManagedFolder folder, string? relativePath = null, bool onlyNewFiles = false, bool skipMylist = false, bool? cleanUpStructure = null)
        => await ScanManagedFolder((ShokoManagedFolder)folder, relativePath, onlyNewFiles, skipMylist, cleanUpStructure);

    private async Task ScanManagedFolder(ShokoManagedFolder folder, string? relativePath = null, bool onlyNewFiles = false, bool skipMylist = false, bool? cleanUpStructure = null)
    {
        cleanUpStructure ??= _settingsProvider.GetSettings().Import.CleanUpStructure;

        var startedAt = DateTime.Now;
        var files = GetFilesInImportFolder(folder);
        if (files.Length == 0)
        {
            _logger.LogInformation("Managed folder is temporarily or permanently unavailable. Aborting scan; {Path} (ManagedFolder={ManagedFolderID})", folder.Path, folder.ID);
            return;
        }

        if (!string.IsNullOrEmpty(relativePath))
        {
            relativePath = PlatformUtility.NormalizePath(relativePath, stripLeadingSlash: true);
            files = files
                .Where(filePath =>
                {
                    var relativeFilePath = PlatformUtility.NormalizePath(filePath[folder.Path.Length..], stripLeadingSlash: true);
                    return relativeFilePath.StartsWith(relativePath, PlatformUtility.StringComparison);
                })
                .ToArray();
        }

        var filesAt = DateTime.Now - startedAt;
        _logger.LogInformation("Managed folder scan started; {Path} (ManagedFolder={ManagedFolderID}RelativePath={RelativePath},Files={FilesCount},FilesScanTime={FilesAt})", folder.Path, folder.ID, relativePath, files.Length, filesAt);
        var existingFiles = new HashSet<string>();
        foreach (var location in folder.Places)
        {
            try
            {
                if (location.Path is not { Length: > 0 } path)
                {
                    _logger.LogInformation("Removing invalid full path for VideoLocal_Place; {Path} (Video={VideoID},Place={PlaceID},ManagedFolder={ManagedFolderID})", location.RelativePath, location.VideoID, location.ID, location.ManagedFolderID);
                    await RemoveRecord(location, updateMyListStatus: !skipMylist);
                    continue;
                }

                if (!location.IsAvailable)
                {
                    _logger.LogInformation("Removing missing path for VideoLocal_Place; {Path} (Video={VideoID},Place={PlaceID},ManagedFolder={ManagedFolderID})", location.RelativePath, location.VideoID, location.ID, location.ManagedFolderID);
                    await RemoveRecord(location, updateMyListStatus: !skipMylist);
                    continue;
                }

                existingFiles.Add(path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An exception occurred while processing VideoLocal_Place; {Path} (Video={VideoID},Place={PlaceID},ManagedFolder={ManagedFolderID})", location.RelativePath, location.VideoID, location.ID, location.ManagedFolderID);
            }
        }

        var filesFound = 0;
        var videosFound = 0;
        var ignoredFiles = _videoLocalRepository.GetIgnoredVideos()
            .SelectMany(a => a.Places)
            .Select(a => a.Path!)
            .Where(a => !string.IsNullOrEmpty(a))
            .ToList();
        var settings = _settingsProvider.GetSettings();
        var scheduler = await _schedulerFactory.GetScheduler();
        files = files
            .Where(filePath => !onlyNewFiles || !existingFiles.Contains(filePath))
            .Except(ignoredFiles, StringComparer.InvariantCultureIgnoreCase)
            .ToArray();
        var total = files.Length;
        var parallelism = Math.Min(settings.Quartz.MaxThreadPoolSize > 0 ? settings.Quartz.MaxThreadPoolSize : Environment.ProcessorCount, Environment.ProcessorCount);
        var actionBlock = new ActionBlock<int>(
            async index =>
            {
                var fileName = files[index];
                var relativePath = PlatformUtility.NormalizePath(fileName[folder.Path.Length..], stripLeadingSlash: true);
                if (++filesFound % 100 == 0 || filesFound == 1 || filesFound == total)
                    _logger.LogTrace("Processing File {Count}/{Total} in folder {FolderName} --- {Name}", filesFound, total, folder.Name, fileName);

                if (!Utils.IsVideo(fileName))
                    return;

                videosFound++;

                await NotifyVideoFileChangeDetected(folder, relativePath, updateMylist: !skipMylist);
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = parallelism,
            }
        );

        _logger.LogDebug("Processing {Count} files in folder {FolderName} with {Parallelism} threads. (Folder={FolderID})", total, folder.Name, parallelism, folder.ID);
        for (var index = 0; index < total; index++)
            await actionBlock.SendAsync(index).ConfigureAwait(false);

        actionBlock.Complete();

        await actionBlock.Completion.ConfigureAwait(false);

        _logger.LogDebug("Found {FileCount} files and {VideoCount} videos in folder {FolderName} in {TimeSpan}. (Folder={FolderID},FilesScanTime={FilesAt})", filesFound, videosFound, folder.Name, DateTime.Now - startedAt, folder.ID, filesAt);

        if (cleanUpStructure.Value)
        {
            var timeStarted = DateTime.Now;
            _logger.LogInformation("Cleaning up managed folder; {Path} (ManagedFolder={ManagedFolderID})", folder.Path, folder.ID);
            CleanupManagedFolder(folder);
            _logger.LogInformation("Cleaned up managed folder in {TimeSpan}; {Path} (ManagedFolder={ManagedFolderID})", DateTime.Now - timeStarted, folder.Path, folder.ID);
        }
    }

    private string[] GetFilesInImportFolder(ShokoManagedFolder folder)
    {
        if (!Directory.Exists(folder.Path))
            return [];
        bool IsMatch(string p, bool isDirectory)
        {
            FileSystemInfo info = isDirectory ? new DirectoryInfo(p) : new FileInfo(p);
            return !_ignoreRules.Any(rule => rule.ShouldIgnore(folder, info));
        }
        return FileSystemHelpers.GetFilePaths(folder.Path, recursive: true, filter: IsMatch);
    }

    public async Task ScheduleScanForManagedFolder(IManagedFolder folder, string? relativePath = null, bool onlyNewFiles = false, bool skipMylist = false, bool? cleanUpStructure = null, bool prioritize = true)
    {
        cleanUpStructure ??= _settingsProvider.GetSettings().Import.CleanUpStructure;

        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<ScanFolderJob>(j => (j.ManagedFolderID, j.RelativePath, j.OnlyNewFiles, j.SkipMyList, j.CleanUpStructure) = (folder.ID, relativePath, onlyNewFiles, skipMylist, cleanUpStructure.Value), prioritize);
    }

    public async Task ScheduleScanForManagedFolders(bool onlyDropSources = false, bool? onlyNewFiles = null, bool skipMylist = false, bool? cleanUpStructure = null, bool prioritize = true)
    {
        cleanUpStructure ??= _settingsProvider.GetSettings().Import.CleanUpStructure;

        var managedFolders = _managedFolderRepository.GetAll();
        var sources = managedFolders.Where(a => a.DropFolderType.HasFlag(DropFolderType.Source)).ToList();
        var rest = onlyDropSources ? [] : managedFolders.Except(sources).ToList();
        if (!onlyNewFiles.HasValue)
        {
            foreach (var source in sources)
                await ScheduleScanForManagedFolder(source, skipMylist: skipMylist, cleanUpStructure: cleanUpStructure, prioritize: prioritize);
            foreach (var folder in rest)
                await ScheduleScanForManagedFolder(folder, onlyNewFiles: true, skipMylist: skipMylist, cleanUpStructure: cleanUpStructure, prioritize: prioritize);
            return;
        }

        foreach (var source in sources)
            await ScheduleScanForManagedFolder(source, skipMylist: skipMylist, cleanUpStructure: cleanUpStructure, prioritize: prioritize);
        foreach (var folder in rest)
            await ScheduleScanForManagedFolder(folder, onlyNewFiles: onlyNewFiles.Value, skipMylist: skipMylist, cleanUpStructure: cleanUpStructure, prioritize: prioritize);
    }

    #endregion Managed Folder

    #region Cleanup

    public void CleanupManagedFolder(IManagedFolder managedFolder)
    {
        var directories = FileSystemHelpers.GetDirectoryPaths(managedFolder.Path, recursive: true);
        RecursiveDeleteEmptyDirectories(directories, managedFolder.Path);
    }

    public void RecursiveDeleteEmptyDirectories(string? toBeChecked, string? directoryToClean)
        => RecursiveDeleteEmptyDirectories([toBeChecked], directoryToClean);

    public void RecursiveDeleteEmptyDirectories(IEnumerable<string?> toBeChecked, string? directoryToClean)
    {
        if (string.IsNullOrEmpty(directoryToClean))
            return;
        try
        {
            directoryToClean = directoryToClean.TrimEnd(Path.DirectorySeparatorChar);
            var directoriesToClean = toBeChecked
                .SelectMany(path =>
                {
                    int? isExcludedAt = null;
                    var paths = new List<(string path, int level)>();
                    while (!string.IsNullOrEmpty(path))
                    {
                        var level = path == directoryToClean ? 0 : path[(directoryToClean.Length + 1)..].Split(Path.DirectorySeparatorChar).Length;
                        if (path == directoryToClean)
                            break;
                        if (Utils.SettingsProvider.GetSettings().Import.ExcludeExpressions.Any(reg => reg.IsMatch(path)))
                            isExcludedAt = level;
                        paths.Add((path, level));
                        path = Path.GetDirectoryName(path);
                    }
                    return isExcludedAt.HasValue
                        ? paths.Where(tuple => tuple.level < isExcludedAt.Value)
                        : paths;
                })
                .DistinctBy(tuple => tuple.path)
                .OrderByDescending(tuple => tuple.level)
                .ThenBy(tuple => tuple.path)
                .Select(tuple => tuple.path)
                .ToList();
            foreach (var directoryPath in directoriesToClean)
            {
                if (Directory.Exists(directoryPath) && IsDirectoryEmpty(directoryPath))
                {
                    _logger.LogTrace("Removing EMPTY directory at {Path}", directoryPath);

                    try
                    {
                        Directory.Delete(directoryPath);
                    }
                    catch (Exception ex)
                    {
                        if (ex is DirectoryNotFoundException or FileNotFoundException) return;
                        _logger.LogWarning(ex, "Unable to DELETE directory: {Directory}", directoryPath);
                    }
                }
            }
        }
        catch (Exception e)
        {
            if (e is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException) return;
            _logger.LogError(e, "There was an error removing the empty directories in {Dir}\n{Ex}", directoryToClean, e);
        }
    }

    private static bool IsDirectoryEmpty(string path)
    {
        try
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region MediaInfo


    double CalculateDurationOggFile(string filename)
    {
        try
        {
            var oggFile = OggFile.ParseFile(filename);
            return oggFile.Duration;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to parse duration from Ogg-Vorbis file {filename}.", filename);
            return 0;
        }
    }

    public bool RefreshMediaInfo(VideoLocal_Place place, VideoLocal video)
    {
        _logger.LogTrace("Getting media info for: {Place}", place.Path ?? place.ID.ToString());
        if (!place.IsAvailable)
        {
            _logger.LogError("File {Place} failed to be retrieved for MediaInfo", place.ID.ToString());
            return false;
        }

        var path = place.Path;
        try
        {
            var mediaInfo = MediaInfoUtility.GetMediaInfo(path);
            if (mediaInfo is { GeneralStream: { Duration: 0, Format: "ogg" } })
                mediaInfo.GeneralStream.Duration = CalculateDurationOggFile(path);

            if (mediaInfo is { IsUsable: true })
            {
                var subs = SubtitleHelper.GetSubtitleStreams(place.Path);
                if (subs.Count > 0)
                    mediaInfo.media.track.AddRange(subs);

                video.MediaInfo = mediaInfo;
                video.MediaVersion = VideoLocal.MEDIA_VERSION;
                return true;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to read the media information of file {Place} ERROR: {Ex}", path,
                e);
        }

        _logger.LogError("File {Place} failed to read MediaInfo", path);
        return false;
    }

    #endregion
}
