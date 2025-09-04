using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#pragma warning disable CS0618
#nullable enable
namespace Shoko.Server.Services.Abstraction;

public class AbstractVideoService : IVideoService
{
    private static readonly StringComparison _platformComparison = Utils.IsLinux
        ? StringComparison.InvariantCulture
        : StringComparison.InvariantCultureIgnoreCase;

    private readonly ILogger<AbstractVideoService> _logger;

    private readonly VideoLocal_PlaceRepository _placeRepository;

    private readonly VideoLocalRepository _videoLocalRepository;

    private readonly VideoLocal_PlaceRepository _videoLocalPlaceRepository;

    private readonly VideoLocal_HashDigestRepository _videoLocalHashRepository;

    private readonly ShokoManagedFolderRepository _managedFolderRepository;

    private readonly CrossRef_File_EpisodeRepository _crossRefRepository;

    private readonly FileNameHashRepository _fileNameHashRepository;

    private readonly VideoHashingService _videoHashingService;

    private readonly IVideoReleaseService _videoReleaseService;

    private readonly VideoLocal_PlaceService _vlpService;

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

    public AbstractVideoService(
        ILogger<AbstractVideoService> logger,
        VideoLocal_PlaceRepository placeRepository,
        VideoLocalRepository videoLocalRepository,
        VideoLocal_PlaceRepository videoLocalPlaceRepository,
        VideoLocal_HashDigestRepository videoLocalHashRepository,
        ShokoManagedFolderRepository managedFolderRepository,
        CrossRef_File_EpisodeRepository crossRefRepository,
        FileNameHashRepository fileNameHashRepository,
        IVideoHashingService videoHashingService,
        IVideoReleaseService videoReleaseService,
        VideoLocal_PlaceService vlpService,
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
        _videoHashingService = (VideoHashingService)videoHashingService;
        _videoReleaseService = videoReleaseService;
        _vlpService = vlpService;
        _schedulerFactory = schedulerFactory;
        _settingsProvider = settingsProvider;
        _databaseFactory = databaseFactory;

        ShokoEventHandler.Instance.FileDeleted += OnFileDeleted;
        _videoHashingService.FileHashed += OnFileHashed;
        ShokoEventHandler.Instance.FileRelocated += OnFileRelocated;
        _managedFolderRepository.ManagedFolderAdded += OnManagedFolderAdded;
        _managedFolderRepository.ManagedFolderUpdated += OnManagedFolderUpdated;
        _managedFolderRepository.ManagedFolderRemoved += OnManagedFolderRemoved;
    }

    ~AbstractVideoService()
    {
        ShokoEventHandler.Instance.FileDeleted -= OnFileDeleted;
        _videoHashingService.FileHashed -= OnFileHashed;
        ShokoEventHandler.Instance.FileRelocated -= OnFileRelocated;
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
            .FirstOrDefault(a => string.Equals(a.Path, absolutePath, _platformComparison));

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
    public async Task NotifyVideoFileChangeDetected(string absolutePath, bool updateMylist = true)
    {
        var (managedFolder, relativePath) = _managedFolderRepository.GetFromAbsolutePath(absolutePath);
        if (managedFolder is null || string.IsNullOrEmpty(relativePath))
            throw new InvalidOperationException($"The path is outside of any managed folders: {absolutePath}");

        await NotifyVideoFileChangeDetected(managedFolder, relativePath, updateMylist);
    }

    /// <inheritdoc/>
    public async Task NotifyVideoFileChangeDetected(IManagedFolder managedFolder, string relativePath, bool updateMylist = true)
    {
        // Don't trust the input to be cleaned beforehand.
        relativePath = Utils.CleanPath(relativePath, cleanStart: true);

        // If some plugin decided to (ab)use this method to scan a path, then forward it to the scan method without cleaning.
        var absolutePath = Path.Join(managedFolder.Path, relativePath);
        if (Directory.Exists(absolutePath))
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
                var originalPath = Utils.EnsureUsablePath(Path.Join(managedFolder.Path, relativePath));
                var resolvedPath = File.ResolveLinkTarget(originalPath, true)?.FullName ?? originalPath;
                if (resolvedPath != originalPath)
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

    public IManagedFolder AddManagedFolder(string path, DropFolderType dropFolderType = DropFolderType.Excluded, bool watchForNewFiles = false)
        => _managedFolderRepository.SaveFolder(new ShokoManagedFolder()
        {
            Path = path,
            DropFolderType = dropFolderType,
            IsWatched = watchForNewFiles,
        });

    public IManagedFolder UpdateManagedFolder(IManagedFolder folder, string? path = null, DropFolderType? dropFolderType = null, bool? watchForNewFiles = null)
    {
        var managedFolder = (ShokoManagedFolder)folder;
        if (path is not null) managedFolder.Path = path;
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

        var affectedSeries = new HashSet<SVR_AnimeSeries>();
        using var session = _databaseFactory.SessionFactory.OpenSession();
        foreach (var vid in videos)
            await _vlpService.RemoveRecordWithOpenTransaction(session, vid, affectedSeries, removeMyList);

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
        var files = folder.Files;
        if (files.Count == 0)
        {
            _logger.LogInformation("Managed folder is temporarily or permanently unavailable. Aborting scan; {Path} (ManagedFolder={ManagedFolderID})", folder.Path, folder.ID);
            return;
        }

        if (!string.IsNullOrEmpty(relativePath))
        {
            relativePath = Utils.CleanPath(relativePath, cleanStart: true);
            files = files
                .Where(filePath =>
                {
                    var relativeFilePath = Utils.CleanPath(filePath[folder.Path.Length..], cleanStart: true);
                    return relativeFilePath.StartsWith(relativePath, Utils.PlatformComparison);
                })
                .ToList();
        }

        var filesAt = DateTime.Now - startedAt;
        _logger.LogInformation("Managed folder scan started; {Path} (ManagedFolder={ManagedFolderID}RelativePath={RelativePath},Files={FilesCount},FilesScanTime={FilesAt})", folder.Path, folder.ID, relativePath, files.Count, filesAt);
        var existingFiles = new HashSet<string>();
        foreach (var location in folder.Places)
        {
            try
            {
                if (location.Path is not { Length: > 0 } path)
                {
                    _logger.LogInformation("Removing invalid full path for VideoLocal_Place; {Path} (Video={VideoID},Place={PlaceID},ManagedFolder={ManagedFolderID})", location.RelativePath, location.VideoID, location.ID, location.ManagedFolderID);
                    await _vlpService.RemoveRecord(location, updateMyListStatus: !skipMylist);
                    continue;
                }

                if (!location.IsAvailable)
                {
                    _logger.LogInformation("Removing missing path for VideoLocal_Place; {Path} (Video={VideoID},Place={PlaceID},ManagedFolder={ManagedFolderID})", location.RelativePath, location.VideoID, location.ID, location.ManagedFolderID);
                    await _vlpService.RemoveRecord(location, updateMyListStatus: !skipMylist);
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
            .Where(filePath =>
            {
                if (settings.Import.Exclude.Any(s => Regex.IsMatch(filePath, s)))
                {
                    _logger.LogTrace("Import exclusion, skipping --- {Name}", filePath);
                    return false;
                }

                return !onlyNewFiles || !existingFiles.Contains(filePath);
            })
            .Except(ignoredFiles, StringComparer.InvariantCultureIgnoreCase)
            .ToList();
        var total = files.Count;
        var parallelism = Math.Min(settings.Quartz.MaxThreadPoolSize > 0 ? settings.Quartz.MaxThreadPoolSize : Environment.ProcessorCount, Environment.ProcessorCount);
        var actionBlock = new ActionBlock<int>(
            async index =>
            {
                var fileName = files[index];
                var relativePath = Utils.CleanPath(fileName[(folder.Path.Length + 1)..], cleanStart: true);
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
            _vlpService.CleanupManagedFolder(folder);
            _logger.LogInformation("Cleaned up managed folder in {TimeSpan}; {Path} (ManagedFolder={ManagedFolderID})", DateTime.Now - timeStarted, folder.Path, folder.ID);
        }
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
}
