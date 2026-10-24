using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using NHibernate;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Events;
using Shoko.Abstractions.Video.Media;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Databases;
using Shoko.Server.MediaInfo;
using Shoko.Server.MediaInfo.Subtitles;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Services.Ogg;
using Shoko.Server.Settings;

#pragma warning disable CS0618
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

    private readonly IVideoRelocationService _relocationService;

    private readonly IQueueScheduler _scheduler;

    private readonly IMylistService _mylistService;

    private readonly ISettingsProvider _settingsProvider;

    private readonly DatabaseFactory _databaseFactory;

    private readonly IFileSystemHelpers _fileSystemHelpers;

    /// <inheritdoc/>
    public event EventHandler<VideoFileDetectedEventArgs>? VideoFileDetected;

    /// <inheritdoc/>
    public event EventHandler<VideoFileEventArgs>? VideoFileDeleted;

    /// <inheritdoc/>
    public event EventHandler<VideoFileHashedEventArgs>? VideoFileHashed;

    /// <inheritdoc/>
    public event EventHandler<VideoFileRelocatedEventArgs>? VideoFileRelocated;

    /// <inheritdoc/>
    public event EventHandler<VideoUpdatedEventArgs>? VideoUpdated;

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
        IVideoRelocationService relocationService,
        IQueueScheduler schedulerFactory,
        IMylistService mylistService,
        ISettingsProvider settingsProvider,
        DatabaseFactory databaseFactory,
        IFileSystemHelpers fileSystemHelpers
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
        _scheduler = schedulerFactory;
        _mylistService = mylistService;
        _settingsProvider = settingsProvider;
        _databaseFactory = databaseFactory;
        _fileSystemHelpers = fileSystemHelpers;

        ShokoEventHandler.Instance.FileDeleted += OnFileDeleted;
        _videoHashingService.VideoFileHashed += OnVideoFileHashed;
        _relocationService.FileRelocated += OnFileRelocated;
        _managedFolderRepository.ManagedFolderAdded += OnManagedFolderAdded;
        _managedFolderRepository.ManagedFolderUpdated += OnManagedFolderUpdated;
        _managedFolderRepository.ManagedFolderRemoved += OnManagedFolderRemoved;
    }

    ~VideoService()
    {
        ShokoEventHandler.Instance.FileDeleted -= OnFileDeleted;
        _videoHashingService.VideoFileHashed -= OnVideoFileHashed;
        _relocationService.FileRelocated -= OnFileRelocated;
        _managedFolderRepository.ManagedFolderAdded -= OnManagedFolderAdded;
        _managedFolderRepository.ManagedFolderUpdated -= OnManagedFolderUpdated;
        _managedFolderRepository.ManagedFolderRemoved -= OnManagedFolderRemoved;
    }

    #region Event Forwarding

    private void OnFileDeleted(object? sender, VideoFileEventArgs eventArgs)
    {
        VideoFileDeleted?.Invoke(this, eventArgs);
    }

    private void OnVideoFileHashed(object? sender, VideoFileHashedEventArgs eventArgs)
    {
        VideoFileHashed?.Invoke(this, eventArgs);
    }

    private void OnFileRelocated(object? sender, VideoFileRelocatedEventArgs eventArgs)
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

    public IReadOnlySet<string> VideoExtensions
    {
        get => _settingsProvider.GetSettings().Import.VideoExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        set => _settingsProvider.GetSettings().Import.VideoExtensions = value.ToList();
    }

    public bool IsAllowedVideoExtension(string fileName)
        => _settingsProvider.GetSettings().Import.VideoExtensions.Any(extName => fileName.EndsWith(extName, StringComparison.OrdinalIgnoreCase));

    #endregion

    #region Video File

    public int MaxAutoScanAttemptsPerVideo
    {
        get => _settingsProvider.GetSettings().Import.MaxAutoScanAttemptsPerFile;
        set
        {
            if (value is < 0 or > 100)
                throw new ArgumentOutOfRangeException(nameof(MaxAutoScanAttemptsPerVideo), value, "Value must be between 0 and 100");
            _settingsProvider.GetSettings().Import.MaxAutoScanAttemptsPerFile = value;
            _settingsProvider.SaveSettings();
        }
    }

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
    public async Task NotifyVideoFileChangeDetected(string absolutePath, bool updateMylist = true, bool forceScan = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(absolutePath);

        var (managedFolder, relativePath) = _managedFolderRepository.GetFromAbsolutePath(absolutePath);
        if (managedFolder is null)
            throw new InvalidOperationException($"The path is outside of any managed folders: {absolutePath}");

        await NotifyVideoFileChangeDetected(managedFolder, relativePath, updateMylist, forceScan);
    }

    /// <inheritdoc/>
    public async Task NotifyVideoFileChangeDetected(IManagedFolder managedFolder, string? relativePath = null, bool updateMylist = true, bool forceScan = false)
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
            await ScanManagedFolder(managedFolder, relativePath: relativePath, onlyNewFiles: false, skipEvents: !updateMylist, cleanUpStructure: false);
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
        var shouldRelocate = hasXrefs && !shouldHash && locationAvailable;
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
            _logger.LogTrace("Saving video record for path: {Path} (ED2K={ED2K}, Size={Size})", absolutePath, video.Hash, video.FileSize);
            _videoLocalRepository.Save(video, true);

            _logger.LogTrace("Saving video file record for path: {Path} (ED2K={ED2K}, Size={Size})", absolutePath, video.Hash, video.FileSize);
            videoLocation.VideoID = video.VideoLocalID;
            _videoLocalPlaceRepository.Save(videoLocation);
        }

        if (shouldHash)
        {
            _logger.LogTrace("Scheduling video hashing for: {Path}", absolutePath);
            try
            {
                await _videoHashingService.ScheduleGetHashesForPath(absolutePath, skipEvents: !updateMylist);
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
            await _relocationService.ScheduleAutoRelocationForVideoFile(videoLocation);
        }

        if (hasXrefs)
        {
            _logger.LogTrace("Found existing video file with hashes and release info: {Path} (ED2K={ED2K}, Size={Size})", absolutePath, video.Hash, video.FileSize);
            return;
        }

        if (!_videoReleaseService.AutoMatchEnabled)
        {
            _logger.LogTrace("Found existing video file with hashes but without release info and auto-match is disabled: {Path} (ED2K={ED2K}, Size={Size})", absolutePath, video.Hash, video.FileSize);
            return;
        }

        if (
            !forceScan &&
            settings.Import.MaxAutoScanAttemptsPerFile > 0 &&
            _videoReleaseService.GetReleaseMatchAttemptsForVideo(video) is { Count: var count } &&
            count > settings.Import.MaxAutoScanAttemptsPerFile
        )
        {
            _logger.LogTrace(
                "Found existing video file with hashes but without release info with more than {MaxAutoScanAttemptsPerFile} attempts: {Path} (Attempts={Attempts}, ED2K={ED2K}, Size={Size})",
                settings.Import.MaxAutoScanAttemptsPerFile,
                absolutePath,
                count,
                video.Hash,
                video.FileSize
            );
            return;
        }

        _logger.LogTrace("Found existing video file with hashes but without release info: {Path} (ED2K={Hash})", absolutePath, video.Hash);
        await _videoReleaseService.ScheduleFindReleaseForVideo(video, skipEvents: !updateMylist);
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
            if (!IsAllowedVideoExtension(relativePath))
            {
                _logger.LogInformation("File does not have an acceptable video extension, skipping: {Path}", relativePath);
                return false;
            }

            // If this is a new record, check if it's a symlink of another known
            // file or if it's a previously known file.
            if (video is { VideoLocalID: 0 })
            {
                var originalPath = PlatformUtility.EnsureUsablePath(Path.Join(managedFolder.Path, relativePath));
                var resolvedPath = File.Exists(originalPath)
                    ? File.ResolveLinkTarget(originalPath, true)?.FullName
                    : null;
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
                        _logger.LogTrace("Found video for symbolic link: {ResolvedPath} (Hash={Hash}, Size={Size})", resolvedPath, resolvedVideo.Hash, resolvedVideo.FileSize);
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
                    if (TryGetVideoFromFileNameHashTable(fileName, fileSize, out var otherVideo))
                    {
                        _logger.LogTrace("Found video for file name and size through FileNameHash table: {ResolvedPath} (Hash={Hash}, Size={Size})", resolvedPath, otherVideo.Hash, fileSize);
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

    public async Task RemoveRecordAndDeletePhysicalFile(VideoLocal_Place place, bool deleteFolder = true, bool skipEvents = false)
    {
        _logger.LogInformation("Deleting video local place record and file: {Place}", place.Path ?? place.ID.ToString());

        var filePath = place.Path!;
        if (!_fileSystemHelpers.FileExists(filePath))
        {
            _logger.LogInformation("Unable to find file. Removing Record: {Place}", filePath ?? place.RelativePath);
            await RemoveRecord(place, skipEvents);
            return;
        }

        try
        {
            _fileSystemHelpers.DeleteFile(filePath);
            DeleteExternalSubtitles(filePath);
        }
        catch (FileNotFoundException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to delete file \'{Place}\': {Ex}", filePath, ex);
            throw;
        }

        if (deleteFolder)
            RecursiveDeleteEmptyDirectories(Path.GetDirectoryName(filePath), place.ManagedFolder!.Path);

        await RemoveRecord(place, skipEvents);
    }

    public async Task RemoveAndDeleteFileWithOpenTransaction(ISession session, VideoLocal_Place place, HashSet<AnimeSeries> seriesToUpdate, bool deleteFolders = true, bool skipEvents = false)
    {
        try
        {
            _logger.LogInformation("Deleting video local place record and file: {Place}", place.Path ?? place.ID.ToString());

            var filePath = place.Path!;
            if (!_fileSystemHelpers.FileExists(filePath))
            {
                _logger.LogInformation("Unable to find file. Removing Record: {FullServerPath}", filePath);
                await RemoveRecordWithOpenTransaction(session, place, seriesToUpdate, skipEvents);
                return;
            }

            try
            {
                _fileSystemHelpers.DeleteFile(filePath);
                DeleteExternalSubtitles(filePath);
            }
            catch (FileNotFoundException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to delete file \'{Place}\': {Ex}", filePath, ex);
                return;
            }

            if (deleteFolders)
                RecursiveDeleteEmptyDirectories(Path.GetDirectoryName(filePath), place.ManagedFolder!.Path);

            await RemoveRecordWithOpenTransaction(session, place, seriesToUpdate, skipEvents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not delete file and remove record for \"{Place}\": {Ex}", place.Path ?? place.ID.ToString(), ex);
        }
    }

    private void DeleteExternalSubtitles(string? originalFileName)
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
                if (!_fileSystemHelpers.FileExists(subPath)) continue;

                try
                {
                    _fileSystemHelpers.DeleteFile(subPath);
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

    public async Task RemoveRecord(VideoLocal_Place place, bool skipEvents = false)
    {
        _logger.LogInformation("Removing VideoLocal_Place record for: {Place}", place.Path ?? place.ID.ToString());
        var seriesToUpdate = new List<AnimeSeries>();
        var v = place.VideoLocal;

        // Decide based on whether any *other* persisted place remains, not on the raw place count.
        // A transient duplicate location (ID 0) handed in by de-duplication is not a member of
        // `v.Places`, so the old `v.Places.Count <= 1` guard mistook the surviving real place for the
        // "last" one and deleted the entire VideoLocal — orphaning the kept copy and emptying the series.
        var isLastPlace = v is not null && !v.Places.Any(p => p.ID != place.ID);
        using (var session = _databaseFactory.SessionFactory.OpenSession())
        {
            if (v is not null && isLastPlace)
            {
                if (!skipEvents)
                    await ScheduleRemovalFromMylist(v);

                try
                {
                    ShokoEventHandler.Instance.OnFileDeleted(place.ManagedFolder, place, v);
                }
                catch
                {
                    // ignore
                }

                {
                    using var transaction = session.BeginTransaction();
                    _videoLocalPlaceRepository.DeleteWithOpenTransaction(session, place);

                    seriesToUpdate.AddRange(
                        v
                            .AnimeEpisodes
                            .DistinctBy(a => a.AnimeSeriesID)
                            .Select(a => a.AnimeSeries)
                            .WhereNotNull()
                    );
                    _videoLocalRepository.DeleteWithOpenTransaction(session, v);
                    transaction.Commit();
                }
            }
            else
            {
                if (v is not null)
                {
                    try
                    {
                        ShokoEventHandler.Instance.OnFileDeleted(place.ManagedFolder, place, v);
                    }
                    catch
                    {
                        // ignore
                    }
                }

                // Only delete a persisted place record. A transient stub (ID 0, e.g. an incoming
                // duplicate being de-duplicated) is not in the database and must not be deleted —
                // doing so previously cascaded into removing the whole VideoLocal.
                if (place.ID != 0)
                {
                    using var transaction = session.BeginTransaction();
                    _videoLocalPlaceRepository.DeleteWithOpenTransaction(session, place);
                    transaction.Commit();
                }
            }
        }

        await Task.WhenAll(seriesToUpdate.Select(a => _scheduler.StartJob<RefreshAnimeStatsJob>(b => b.AnimeID = a.AniDB_ID)));
    }

    public async Task RemoveRecordWithOpenTransaction(ISession session, VideoLocal_Place place, ICollection<AnimeSeries> seriesToUpdate,
        bool skipEvents = false)
    {
        _logger.LogInformation("Removing VideoLocal_Place record for: {Place}", place.Path ?? place.ID.ToString());
        var v = place.VideoLocal;

        if (v?.Places?.Count <= 1)
        {
            if (!skipEvents)
                await ScheduleRemovalFromMylist(v);

            var eps = v.AnimeEpisodes?.WhereNotNull().ToList();
            eps?.DistinctBy(a => a.AnimeSeriesID).Select(a => a.AnimeSeries).WhereNotNull().ToList().ForEach(seriesToUpdate.Add);

            try
            {
                ShokoEventHandler.Instance.OnFileDeleted(place.ManagedFolder, place, v);
            }
            catch
            {
                // ignore
            }

            {
                using var transaction = session.BeginTransaction();
                _videoLocalPlaceRepository.DeleteWithOpenTransaction(session, place);
                _videoLocalRepository.DeleteWithOpenTransaction(session, v);
                transaction.Commit();
            }
        }
        else
        {
            if (v is not null)
            {
                try
                {
                    ShokoEventHandler.Instance.OnFileDeleted(place.ManagedFolder, place, v);
                }
                catch
                {
                    // ignore
                }
            }

            {
                using var transaction = session.BeginTransaction();
                _videoLocalPlaceRepository.DeleteWithOpenTransaction(session, place);
                transaction.Commit();
            }
        }
    }

    public Task ScheduleRemovalFromMylist(VideoLocal video)
        => _mylistService.ScheduleDisposeVideo(video);

    #endregion

    #endregion Video File

    #region Video

    /// <inheritdoc/>
    public IEnumerable<IVideo> GetAllVideos()
        => _videoLocalRepository.GetAll().AsQueryable();

    /// <inheritdoc/>
    public IReadOnlyList<IVideo> GetIgnoredVideos()
        => _videoLocalRepository.GetIgnoredVideos();

    /// <inheritdoc/>
    public IReadOnlyList<IVideo> GetUnrecognizedVideos(bool includeBrokenLinks = false)
        => _videoLocalRepository.GetVideosWithoutEpisode(includeBrokenLinks);

    /// <inheritdoc/>
    public IReadOnlyList<IVideo> GetVideosWithMissingFiles(bool onlyFullyMissing = false)
        => _videoLocalRepository.GetAll()
            .Where(video => video.Places is var places && (places.Count is 0 || (onlyFullyMissing
                ? places.All(place => !_fileSystemHelpers.FileExists(place.Path))
                : places.Any(place => !_fileSystemHelpers.FileExists(place.Path)))))
            .ToList();

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

    /// <inheritdoc/>
    public IVideo SetVideoVariation(IVideo video, bool isVariation)
    {
        ArgumentNullException.ThrowIfNull(video);

        var videoLocal = _videoLocalRepository.GetByID(video.LocalID)
            ?? throw new ArgumentException("The video is not stored in Shoko.", nameof(video));
        if (videoLocal.IsVariation == isVariation)
            return videoLocal;

        videoLocal.IsVariation = isVariation;
        _videoLocalRepository.Save(videoLocal, false);
        RaiseVideoUpdated(videoLocal, VideoChanges.Variation);
        return videoLocal;
    }

    /// <inheritdoc/>
    public IVideo SetVideoIgnored(IVideo video, bool isIgnored)
    {
        ArgumentNullException.ThrowIfNull(video);

        var videoLocal = _videoLocalRepository.GetByID(video.LocalID)
            ?? throw new ArgumentException("The video is not stored in Shoko.", nameof(video));
        if (videoLocal.IsIgnored == isIgnored)
            return videoLocal;

        videoLocal.IsIgnored = isIgnored;
        _videoLocalRepository.Save(videoLocal, false);
        RaiseVideoUpdated(videoLocal, VideoChanges.Ignored);
        return videoLocal;
    }

    private void RaiseVideoUpdated(VideoLocal video, VideoChanges changes)
    {
        try
        {
            VideoUpdated?.Invoke(this, new() { Video = video, Changes = changes, Actor = ActorContext.CurrentActor });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Got an error in a VideoUpdated event.");
        }
    }

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

    public async Task RemoveMissingVideoFiles(bool removeFromMylist = true, IProgress<decimal>? progress = null, CancellationToken token = default)
    {
        _logger.LogInformation("Remove Missing Files: Start");
        var stages = new StagedProgress(progress, 4, 1, 4, 1);
        stages.Report(0);
        var seriesToUpdate = new HashSet<AnimeSeries>();
        try
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();

            // remove missing files in valid managed folders
            var filesAll = _videoLocalPlaceRepository.GetAll()
                .Where(a => a.ManagedFolder is not null)
                .GroupBy(a => a.ManagedFolder!)
                .SelectMany(a => a)
                .ToList();
            var placeItems = new ItemProgress(stages, filesAll.Count);
            foreach (var vl in filesAll)
            {
                token.ThrowIfCancellationRequested();
                placeItems.Increment();
                if (_fileSystemHelpers.FileExists(vl.Path)) continue;

                // delete video local record
                _logger.LogInformation("Removing Missing File: {ID}", vl.VideoID);
                // skipEvents covers the secondary events — outward side effects, the
                // MyList removal among them — rather than first-party ones like the
                // file-deleted event, which fire either way. So it inverts this flag
                await RemoveRecordWithOpenTransaction(session, vl, seriesToUpdate, skipEvents: !removeFromMylist);
            }

            stages.NextStage();
            token.ThrowIfCancellationRequested();
            var videoLocalsAll = _videoLocalRepository.GetAll().ToList();
            // remove empty video locals
            {
                using var transaction = session.BeginTransaction();
                _videoLocalRepository.DeleteWithOpenTransaction(session, videoLocalsAll.Where(a => a.IsEmpty()).ToList());
                transaction.Commit();
            }

            // Remove duplicate video locals
            var locals = videoLocalsAll
                .Where(a => !string.IsNullOrWhiteSpace(a.Hash))
                .GroupBy(a => a.Hash)
                .ToDictionary(g => g.Key, g => g.ToList());
            var toRemove = new List<VideoLocal>();
            var comparer = new VideoLocalComparer();

            foreach (var hash in locals.Keys)
            {
                var values = locals[hash].ToList();
                values.Sort(comparer);
                var to = values.First();
                values.Remove(to);
                foreach (var places in values.Select(from => from.Places).Where(places => places != null && places.Count != 0))
                {
                    using var transaction = session.BeginTransaction();
                    foreach (var place in places)
                    {
                        place.VideoID = to.VideoLocalID;
                        _videoLocalPlaceRepository.SaveWithOpenTransaction(session, place);
                    }

                    transaction.Commit();
                }

                toRemove.AddRange(values);
            }

            {
                using var transaction = session.BeginTransaction();
                foreach (var remove in toRemove)
                {
                    _videoLocalRepository.DeleteWithOpenTransaction(session, remove);
                }

                transaction.Commit();
            }

            // Remove files in invalid managed folders
            stages.NextStage();
            var videoItems = new ItemProgress(stages, videoLocalsAll.Count);
            foreach (var v in videoLocalsAll)
            {
                token.ThrowIfCancellationRequested();
                videoItems.Increment();
                var places = v.Places;
                if (places.Count > 0)
                {
                    using var transaction = session.BeginTransaction();
                    foreach (var place in places.Where(place => string.IsNullOrWhiteSpace(place?.Path)))
                    {
#pragma warning disable CS0618
                        _logger.LogInformation("Remove Records With Orphaned Managed Folder: {Filename}", v.FileName);
#pragma warning restore CS0618
                        seriesToUpdate.UnionWith(v.AnimeEpisodes.Select(a => a.AnimeSeries).WhereNotNull().DistinctBy(a => a.AnimeSeriesID));
                        _videoLocalPlaceRepository.DeleteWithOpenTransaction(session, place);
                    }

                    transaction.Commit();
                }

                // Remove duplicate places
                places = v.Places;
                if (places.Count == 1) continue;

                if (places.Count > 0)
                {
                    places = places.DistinctBy(a => a.Path).ToList();
                    places = v.Places.Except(places).ToList() ?? [];
                    foreach (var place in places)
                    {
                        using var transaction = session.BeginTransaction();
                        _videoLocalPlaceRepository.DeleteWithOpenTransaction(session, place);
                        transaction.Commit();
                    }
                }

                if (v.Places.Count > 0) continue;

                // delete video local record
#pragma warning disable CS0618
                _logger.LogInformation("RemoveOrphanedVideoLocal : {Filename}", v.FileName);
#pragma warning restore CS0618
                seriesToUpdate.UnionWith(v.AnimeEpisodes.Select(a => a.AnimeSeries).WhereNotNull().DistinctBy(a => a.AnimeSeriesID));

                if (removeFromMylist)
                    await ScheduleRemovalFromMylist(v);

                {
                    using var transaction = session.BeginTransaction();
                    _videoLocalRepository.DeleteWithOpenTransaction(session, v);
                    transaction.Commit();
                }
            }

            // Clean up failed imports
            stages.NextStage();
            token.ThrowIfCancellationRequested();
            var list = _videoLocalRepository.GetAll()
                .SelectMany(a => a.EpisodeCrossReferences)
                .Where(a => a.AniDBAnime == null || a.AniDBEpisode == null)
                .ToArray();
            {
                using var transaction = session.BeginTransaction();
                foreach (var xref in list)
                {
                    // We don't need to update anything since they don't exist
                    _crossRefRepository.DeleteWithOpenTransaction(session, xref);
                }

                transaction.Commit();
            }

            // clean up orphaned video local places
            var placesToRemove = _videoLocalPlaceRepository.GetAll().Where(a => a.VideoLocal == null).ToList();
            {
                using var transaction = session.BeginTransaction();
                foreach (var place in placesToRemove)
                {
                    // We don't need to update anything since they don't exist
                    _videoLocalPlaceRepository.DeleteWithOpenTransaction(session, place);
                }

                transaction.Commit();
            }

            // NOTE: use 'purge unused releases' if you want to remove the cross-references too.
        }
        finally
        {
            // update everything we modified
            await Task.WhenAll(seriesToUpdate.Select(a => _scheduler.StartJob<RefreshAnimeStatsJob>(b => b.AnimeID = a.AniDB_ID)));
        }

        stages.Complete();
        _logger.LogInformation("Remove Missing Files: Finished");
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

    public IManagedFolder AddManagedFolder(ManagedFolderData folderData)
        => _managedFolderRepository.SaveFolder(new ShokoManagedFolder()
        {
            Name = folderData.Name,
            Path = folderData.Path,
            DropFolderType = folderData.DropFolderType,
            IsWatched = folderData.WatchForNewFiles,
        });

    public IManagedFolder UpdateManagedFolder(IManagedFolder folder, ManagedFolderUpdateData folderUpdate)
    {
        // The save compares the changes against the cached folder, so they go on a copy.
        var current = _managedFolderRepository.GetByID(folder.ID)
            ?? throw new ArgumentException("The managed folder is not stored in Shoko.", nameof(folder));
        var managedFolder = new ShokoManagedFolder
        {
            ID = current.ID,
            Name = folderUpdate.Name is { Length: > 0 } ? folderUpdate.Name : current.Name,
            Path = folderUpdate.Path is { Length: > 0 } ? folderUpdate.Path : current.Path,
            DropFolderType = folderUpdate.DropFolderType ?? current.DropFolderType,
            IsWatched = folderUpdate.WatchForNewFiles ?? current.IsWatched,
        };
        return _managedFolderRepository.SaveFolder(managedFolder);
    }

    public async Task RemoveManagedFolder(IManagedFolder folder, bool keepRecords = false, bool skipEvents = false)
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
            await RemoveRecordWithOpenTransaction(session, vid, affectedSeries, skipEvents);

        _managedFolderRepository.Delete(folder.ID);
        await Task.WhenAll(affectedSeries.Select(a => _scheduler.StartJob<RefreshAnimeStatsJob>(b => b.AnimeID = a.AniDB_ID)));
    }

    public async Task ScanManagedFolder(IManagedFolder folder, string? relativePath = null, bool onlyNewFiles = false, bool skipEvents = false, bool? cleanUpStructure = null, bool? checkFileSize = null, bool forceScan = false)
        => await ScanManagedFolder((ShokoManagedFolder)folder, relativePath, onlyNewFiles, skipEvents, cleanUpStructure, checkFileSize, forceScan, null, CancellationToken.None);

    /// <summary>
    /// Scans a managed folder, as <see cref="ScanManagedFolder(IManagedFolder, string?, bool, bool, bool?, bool?, bool)"/>
    /// does, reporting progress and stopping when cancelled.
    /// </summary>
    /// <param name="folder">The managed folder to scan.</param>
    /// <param name="relativePath">Only scan below this path in the folder, when set.</param>
    /// <param name="onlyNewFiles">Only look at files that have no record yet.</param>
    /// <param name="skipEvents">Skip the events and the MyList updates.</param>
    /// <param name="cleanUpStructure">Remove empty folders afterwards; the import setting when <c>null</c>.</param>
    /// <param name="checkFileSize">Look again at files whose size changed; the import setting when <c>null</c>.</param>
    /// <param name="forceScan">Look at every file again, even known ones.</param>
    /// <param name="progress">Told how far the scan is, as a percentage from 0 to 100.</param>
    /// <param name="token">Stops the scan; files already looked at stay looked at.</param>
    /// <returns>A task that completes once the scan is done.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
    internal Task ScanManagedFolder(IManagedFolder folder, string? relativePath, bool onlyNewFiles, bool skipEvents, bool? cleanUpStructure, bool? checkFileSize, bool forceScan, IProgress<decimal>? progress, CancellationToken token)
        => ScanManagedFolder((ShokoManagedFolder)folder, relativePath, onlyNewFiles, skipEvents, cleanUpStructure, checkFileSize, forceScan, progress, token);

    private async Task ScanManagedFolder(ShokoManagedFolder folder, string? relativePath, bool onlyNewFiles, bool skipEvents, bool? cleanUpStructure, bool? checkFileSize, bool forceScan, IProgress<decimal>? progress, CancellationToken token)
    {
        progress?.Report(0);
        cleanUpStructure ??= _settingsProvider.GetSettings().Import.CleanUpStructure;
        checkFileSize ??= _settingsProvider.GetSettings().Import.CheckFileSize;

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
        _logger.LogInformation("Managed folder scan started; {Path} (ManagedFolder={ManagedFolderID}, RelativePath={RelativePath}, Files={FilesCount}, FilesScanTime={FilesAt})", folder.Path, folder.ID, relativePath, files.Length, filesAt);
        var existingFiles = new ConcurrentDictionary<string, long>();
        foreach (var location in folder.Places)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (location.Path is not { Length: > 0 } path)
                {
                    _logger.LogInformation("Removing invalid full path for VideoLocal_Place; {Path} (Video={VideoID}, Place={PlaceID}, ManagedFolder={ManagedFolderID})", location.RelativePath, location.VideoID, location.ID, location.ManagedFolderID);
                    await RemoveRecord(location, skipEvents: skipEvents);
                    continue;
                }

                if (!location.IsAvailable)
                {
                    _logger.LogInformation("Removing missing path for VideoLocal_Place; {Path} (Video={VideoID}, Place={PlaceID}, ManagedFolder={ManagedFolderID})", location.RelativePath, location.VideoID, location.ID, location.ManagedFolderID);
                    await RemoveRecord(location, skipEvents: skipEvents);
                    continue;
                }

                if (location.VideoLocal is not { } video)
                {
                    _logger.LogInformation("Removing orphaned VideoLocal_Place; {Path} (Video={VideoID}, Place={PlaceID}, ManagedFolder={ManagedFolderID})", location.RelativePath, location.VideoID, location.ID, location.ManagedFolderID);
                    await RemoveRecord(location, skipEvents: true);
                    continue;
                }

                existingFiles.TryAdd(path, video.FileSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An exception occurred while processing VideoLocal_Place; {Path} (Video={VideoID}, Place={PlaceID}, ManagedFolder={ManagedFolderID})", location.RelativePath, location.VideoID, location.ID, location.ManagedFolderID);
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
        if (checkFileSize.Value && !onlyNewFiles)
        {
            files = files
                .Except(ignoredFiles, StringComparer.InvariantCultureIgnoreCase)
                .Where(filePath => !existingFiles.ContainsKey(filePath) || existingFiles[filePath] != _fileSystemHelpers.GetFileSize(filePath))
                .ToArray();
        }
        else
        {
            files = files
                .Where(filePath => !onlyNewFiles || !existingFiles.ContainsKey(filePath))
                .Except(ignoredFiles, StringComparer.InvariantCultureIgnoreCase)
                .ToArray();
        }
        var total = files.Length;
        var processed = 0;
        var parallelism = Math.Min(settings.Queue.MaxTotalWorkers > 0 ? settings.Queue.MaxTotalWorkers : Environment.ProcessorCount, Environment.ProcessorCount);
        var actionBlock = new ActionBlock<int>(
            async index =>
            {
                try
                {
                    var fileName = files[index];
                    var relativePath = PlatformUtility.NormalizePath(fileName[folder.Path.Length..], stripLeadingSlash: true);
                    if (++filesFound % 100 == 0 || filesFound == 1 || filesFound == total)
                        _logger.LogTrace("Processing File {Count}/{Total} in folder {FolderName} --- {Name}", filesFound, total, folder.Name, fileName);

                    if (!IsAllowedVideoExtension(fileName))
                        return;

                    videosFound++;

                    await NotifyVideoFileChangeDetected(folder, relativePath, updateMylist: !skipEvents, forceScan: forceScan);
                }
                finally
                {
                    progress?.Report(100m * Interlocked.Increment(ref processed) / total);
                }
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = parallelism,
                CancellationToken = token,
            }
        );

        _logger.LogDebug("Processing {Count} files in folder {FolderName} with {Parallelism} threads. (Folder={FolderID})", total, folder.Name, parallelism, folder.ID);
        for (var index = 0; index < total; index++)
            await actionBlock.SendAsync(index, token);

        actionBlock.Complete();

        await actionBlock.Completion;

        _logger.LogDebug("Found {FileCount} files and {VideoCount} videos in folder {FolderName} in {TimeSpan}. (Folder={FolderID}, FilesScanTime={FilesAt})", filesFound, videosFound, folder.Name, DateTime.Now - startedAt, folder.ID, filesAt);

        if (cleanUpStructure.Value)
        {
            var timeStarted = DateTime.Now;
            _logger.LogInformation("Cleaning up managed folder; {Path} (ManagedFolder={ManagedFolderID})", folder.Path, folder.ID);
            CleanupManagedFolder(folder);
            _logger.LogInformation("Cleaned up managed folder in {TimeSpan}; {Path} (ManagedFolder={ManagedFolderID})", DateTime.Now - timeStarted, folder.Path, folder.ID);
        }

        progress?.Report(100);
    }

    private string[] GetFilesInImportFolder(ShokoManagedFolder folder)
    {
        if (!_fileSystemHelpers.DirectoryExists(folder.Path))
            return [];
        bool IsMatch(string p, bool isDirectory)
        {
            FileSystemInfo info = isDirectory ? new DirectoryInfo(p) : new FileInfo(p);
            return !_ignoreRules.Any(rule => rule.ShouldIgnore(folder, info));
        }
        return _fileSystemHelpers.GetFilePaths(folder.Path, recursive: true, filter: IsMatch);
    }

    public async Task ScheduleScanForManagedFolder(IManagedFolder folder, string? relativePath = null, bool onlyNewFiles = false, bool skipEvents = false, bool? cleanUpStructure = null, bool? checkFileSize = null, bool forceScan = false, bool prioritize = true)
    {
        cleanUpStructure ??= _settingsProvider.GetSettings().Import.CleanUpStructure;
        checkFileSize ??= _settingsProvider.GetSettings().Import.CheckFileSize;
        await _scheduler.StartJob<ScanFolderJob>(j =>
            (j.ManagedFolderID, j.RelativePath, j.OnlyNewFiles, j.SkipEvents, j.CleanUpStructure, j.CheckFileSize, j.ForceScan) =
            (folder.ID, relativePath ?? string.Empty, onlyNewFiles, skipEvents, cleanUpStructure.Value, checkFileSize.Value, forceScan),
            prioritize
        );
    }

    /// <inheritdoc />
    public async Task ScheduleScanForManagedFolders(
        bool onlyDropSources = false,
        bool? onlyNewFiles = null,
        bool skipEvents = false,
        bool? cleanUpStructure = null,
        bool forceScan = false,
        bool prioritize = true,
        IProgress<decimal>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        cleanUpStructure ??= _settingsProvider.GetSettings().Import.CleanUpStructure;

        var managedFolders = _managedFolderRepository.GetAll();
        var sources = managedFolders.Where(a => a.DropFolderType.HasFlag(DropFolderType.Source)).ToList();
        var rest = onlyDropSources ? [] : managedFolders.Except(sources).ToList();
        var items = new ItemProgress(progress, sources.Count + rest.Count);
        items.Report(0);
        foreach (var folder in sources.Concat(rest))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Left unsaid, the drop sources are scanned whole and the rest for new files only.
            var newFilesOnly = onlyNewFiles ?? !sources.Contains(folder);
            await ScheduleScanForManagedFolder(
                folder,
                onlyNewFiles: newFilesOnly,
                skipEvents: skipEvents,
                cleanUpStructure: cleanUpStructure,
                forceScan: forceScan,
                prioritize: prioritize
            );
            items.Increment();
        }
    }

    #endregion Managed Folder

    #region Cleanup

    public void CleanupManagedFolder(IManagedFolder managedFolder)
    {
        var directories = _fileSystemHelpers.GetDirectoryPaths(managedFolder.Path, recursive: true);
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
            // Path.TrimEndingDirectorySeparator, unlike TrimEnd, leaves a drive/volume root's
            // separator intact (e.g. "C:\" stays "C:\" instead of becoming "C:"). Path.GetDirectoryName
            // always returns a root with its trailing separator, so stripping it here would make the
            // `path == directoryToClean` break check below never match for a root-level managed folder,
            // letting the root itself leak into the deletion candidate list.
            directoryToClean = Path.TrimEndingDirectorySeparator(directoryToClean);
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
                        if (ISettingsProvider.Instance.GetSettings().Import.ExcludeExpressions.Any(reg => reg.IsMatch(path)))
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
                if (_fileSystemHelpers.DirectoryExists(directoryPath) && IsDirectoryEmpty(directoryPath))
                {
                    _logger.LogTrace("Removing EMPTY directory at {Path}", directoryPath);

                    try
                    {
                        _fileSystemHelpers.DeleteDirectory(directoryPath);
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
            return !Directory.EnumerateFileSystemEntries(PlatformUtility.EnsureUsablePath(path)).Any();
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Media Info

    public IMediaInfo? ReadMediaInfoFromPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        var mediaInfo = MediaInfoUtility.GetMediaInfo(path);
        if (mediaInfo is { GeneralStream: { Duration: 0, Format: "ogg" } })
            mediaInfo.GeneralStream.Duration = CalculateDurationOggFile(path);

        if (mediaInfo is { IsUsable: true })
        {
            var subs = SubtitleHelper.GetSubtitleStreams(path);
            if (subs.Count > 0)
                mediaInfo.media!.track!.AddRange(subs);

            return mediaInfo;
        }

        return null;
    }

    public IMediaInfo? ConvertJsonToMediaInfo(string? json)
        => ConvertMediaInfo(json, null);

    private IMediaInfo? ConvertMediaInfo(string? json, string? path)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        var mediaInfo = MediaInfoUtility.GetMediaInfoForJson(json);
        if (mediaInfo is { GeneralStream: { Duration: 0, Format: "ogg" } } && !string.IsNullOrEmpty(path))
            mediaInfo.GeneralStream.Duration = CalculateDurationOggFile(path);

        if (mediaInfo is { IsUsable: true })
        {
            var subs = !string.IsNullOrEmpty(path) ? SubtitleHelper.GetSubtitleStreams(path) : [];
            if (subs.Count > 0)
                mediaInfo.media!.track!.AddRange(subs);

            return mediaInfo;
        }

        return null;
    }

    double CalculateDurationOggFile(string filename)
    {
        try
        {
            var oggFile = OggFile.ParseFile(filename);
            return oggFile?.Duration ?? 0;
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
                var subs = SubtitleHelper.GetSubtitleStreams(path);
                if (subs.Count > 0)
                    mediaInfo.media!.track!.AddRange(subs);

                video.MediaInfo = mediaInfo;
                video.MediaVersion = VideoLocal.MEDIA_VERSION;
                return true;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to read the media information of file {Place} ERROR: {Ex}", path, e);
        }

        _logger.LogError("File {Place} failed to read MediaInfo", path);
        return false;
    }

    #endregion

    #region Static Helpers

    [return: NotNullIfNotNull(nameof(fullPath))]
    public static string? GetDistinctPath(string? fullPath)
    {
        if (string.IsNullOrEmpty(fullPath))
            return null;

        var parent = Path.GetDirectoryName(fullPath);
        return string.IsNullOrEmpty(parent)
            ? fullPath
            : Path.Combine(Path.GetFileName(parent), Path.GetFileName(fullPath));
    }

    #endregion
}
