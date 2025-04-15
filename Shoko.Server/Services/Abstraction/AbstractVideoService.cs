using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories.Cached;
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

    private readonly IVideoHashingService _videoHashingService;

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
        _videoHashingService = videoHashingService;
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
    public async Task NotifyVideoFileChangeDetected(string absolutePath, bool addToMylist = true)
    {
        var (managedFolder, relativePath) = _managedFolderRepository.GetFromAbsolutePath(absolutePath);
        if (managedFolder is null || string.IsNullOrEmpty(relativePath))
            throw new InvalidOperationException($"The path is outside of any managed folders: {absolutePath}");

        await NotifyVideoFileChangeDetected(managedFolder, relativePath, addToMylist);
    }

    /// <inheritdoc/>
    public async Task NotifyVideoFileChangeDetected(IManagedFolder managedFolder, string relativePath, bool addToMylist = true)
    {
        if (!Utils.IsVideo(relativePath)) return;
        relativePath = Utils.CleanPath(relativePath, cleanStart: true);
        var absolutePath = Path.Join(managedFolder.Path, relativePath);
        var (video, vlp) = GetVideoLocal(managedFolder, relativePath);
        if (video == null || vlp == null)
        {
            _logger.LogWarning("Could not get VideoLocal. exiting");
            return;
        }

        // Dispatch event for newly detected files.
        var vlpAvailable = vlp.IsAvailable;
        if ((video.VideoLocalID is 0 || vlp.ID is 0) && vlpAvailable)
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

        var filename = vlp.FileName;
        var enabledHashes = _videoHashingService.AllEnabledHashTypes;
        var shouldSave = false;
        var shouldHash = string.IsNullOrEmpty(video.Hash) || video.FileSize == 0 || (video.Hashes is { } hashes && (hashes.Count == 0 || enabledHashes.Any(a => !hashes.Any(b => b.Type == a))));
        var hasXrefs = video.EpisodeCrossReferences is { Count: > 0 } xrefs;
        if (vlpAvailable && !shouldHash && hasXrefs && !video.DateTimeImported.HasValue)
        {
            video.DateTimeImported = DateTime.Now;
            shouldSave = true;
        }

        // this can't run on a new file, because shouldHash is true
        if (vlpAvailable && !shouldHash && !shouldSave)
        {
            if (hasXrefs)
            {
                _logger.LogTrace("Found existing video file with hashes and release info: {File} (ED2K={Hash})", absolutePath, video.Hash);
                return;
            }

            // Don't schedule the auto-match attempt if auto-matching is disabled.
            if (!_videoReleaseService.AutoMatchEnabled)
            {
                _logger.LogTrace("Found existing video file with hashes but without release info and auto-match is disabled: {File} (ED2K={Hash})", absolutePath, video.Hash);
                return;
            }

            _logger.LogTrace("Found existing video file with hashes but without release info: {File} (ED2K={Hash})", absolutePath, video.Hash);
            await _videoReleaseService.ScheduleFindReleaseForVideo(video, addToMylist: addToMylist);
            return;
        }

        // process duplicate import
        var duplicateRemoved = await ProcessDuplicates(video, vlp, vlpAvailable);
        // it was removed. Don't try to hash or save
        if (duplicateRemoved) return;

        if (shouldSave)
        {
            _logger.LogTrace("Saving VideoLocal: VideoLocalID: {VideoLocalID},  Filename: {FileName}, Hash: {Hash}", video.VideoLocalID, absolutePath, video.Hash);
            _videoLocalRepository.Save(video, true);

            _logger.LogTrace("Saving VideoLocal_Place: VideoLocal_Place_ID: {PlaceID}, Path: {Path}", vlp.ID, absolutePath);
            vlp.VideoID = video.VideoLocalID;
            _videoLocalPlaceRepository.Save(vlp);
        }

        if (shouldHash)
        {
            await _videoHashingService.ScheduleGetHashesForPath(absolutePath, skipMylist: !addToMylist);
            return;
        }

        // Only schedule the auto-match attempt if auto-matching is enabled.
        if (!hasXrefs)
        {
            if (!_videoReleaseService.AutoMatchEnabled)
            {
                _logger.LogTrace("Hashes were found and xrefs are missing, but auto-match is disabled. Exiting: {File}, Hash: {Hash}", absolutePath, video.Hash);
                return;
            }

            _logger.LogTrace("Hashes were found, but xrefs are missing. Queuing a rescan for: {File}, Hash: {Hash}", absolutePath, video.Hash);
            await _videoReleaseService.ScheduleFindReleaseForVideo(video, addToMylist: addToMylist);
        }
    }

    private (VideoLocal?, VideoLocal_Place?) GetVideoLocal(IManagedFolder managedFolder, string relativePath)
    {
        // check if we have already processed this file
        VideoLocal? vlocal = null;
        var vlp = _videoLocalPlaceRepository.GetByRelativePathAndManagedFolderID(relativePath, managedFolder.ID);
        if (vlp != null)
        {
            vlocal = vlp.VideoLocal;
            if (vlocal != null)
            {
                _logger.LogTrace("VideoLocal record found in database: {Filename}", relativePath);

                // This will only happen with DB corruption, so just clean up the mess.
                if (vlp.Path == null)
                {
                    _logger.LogTrace("VideoLocal_Place path is non-existent, removing it");
                    if (vlocal.Places.Count == 1)
                    {
                        _videoLocalRepository.Delete(vlocal);
                        vlocal = null;
                    }

                    _videoLocalPlaceRepository.Delete(vlp);
                    vlp = null;
                }
            }
        }

        if (vlocal == null)
        {
            _logger.LogTrace("No existing VideoLocal, creating temporary record");
            vlocal = new VideoLocal
            {
                DateTimeUpdated = DateTime.Now,
                DateTimeCreated = DateTime.Now,
                FileName = Path.GetFileName(relativePath),
                Hash = string.Empty,
            };
        }

        if (vlp == null)
        {
            _logger.LogTrace("No existing VideoLocal_Place, creating a new record");
            vlp = new VideoLocal_Place
            {
                RelativePath = relativePath,
                ManagedFolderID = managedFolder.ID,
            };
            if (vlocal.VideoLocalID != 0) vlp.VideoID = vlocal.VideoLocalID;
        }

        return (vlocal, vlp);
    }

    private async Task<bool> ProcessDuplicates(VideoLocal video, VideoLocal_Place vlp, bool vlpAvailable)
    {
        // If the VideoLocalID == 0, then it's a new file that wasn't merged after hashing, so it can't be a dupe
        if (video.VideoLocalID == 0) return false;

        // remove missing files
        var preps = video.Places.Where(a =>
        {
            if (vlp.ID == a.ID && vlpAvailable) return false;
            return !a.IsAvailable;
        }).ToList();
        foreach (var vlp1 in preps)
            await _vlpService.RemoveRecord(vlp1);

        var dupPlace = video.Places.FirstOrDefault(a => vlp.ID != a.ID);
        if (dupPlace == null)
        {
            _logger.LogWarning("Removed Remaining File");
            _logger.LogWarning("---------------------------------------------");
            _logger.LogWarning("File: {FullServerPath}", vlp.Path);
            _logger.LogWarning("---------------------------------------------");
            return true;
        }

        _logger.LogWarning("Found Duplicate File");
        _logger.LogWarning("---------------------------------------------");
        _logger.LogWarning("New File: {FullServerPath}", vlp.Path);
        _logger.LogWarning("Existing File: {FullServerPath}", dupPlace.Path);
        _logger.LogWarning("---------------------------------------------");

        var settings = _settingsProvider.GetSettings();
        if (!settings.Import.AutomaticallyDeleteDuplicatesOnImport) return false;

        await _vlpService.RemoveRecordAndDeletePhysicalFile(vlp);
        return true;
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

    public async Task ScanManagedFolder(IManagedFolder folder, bool onlyNewFiles = false, bool skipMylist = false)
        => await ScanManagedFolder((ShokoManagedFolder)folder, onlyNewFiles, skipMylist);

    private async Task ScanManagedFolder(ShokoManagedFolder folder, bool onlyNewFiles = false, bool skipMylist = false)
    {
        var existingFiles = new HashSet<string>();
        foreach (var location in folder.Places)
        {
            try
            {
                if (location.Path is not { Length: > 0 } path)
                {
                    _logger.LogInformation("Removing invalid full path for VideoLocal_Place; {Path} (Video={VideoID},Place={PlaceID},ManagedFolder={ManagedFolderID})", location.RelativePath, location.VideoID, location.ID, location.ManagedFolderID);
                    await _vlpService.RemoveRecord(location);
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
                _logger.LogTrace("Processing File {Count}/{Total} in folder {FolderName} --- {Name}", filesFound, total, folder.Name, fileName);

            if (!Utils.IsVideo(fileName))
                continue;

            videosFound++;

            await NotifyVideoFileChangeDetected(fileName, addToMylist: !skipMylist);
        }

        _logger.LogDebug("Found {Count} files in folder {FolderName}. (Folder={FolderID})", filesFound, folder.Name, folder.ID);
        _logger.LogDebug("Found {Count} videos in folder {FolderName}. (Folder={FolderID})", videosFound, folder.Name, folder.ID);
    }



    public async Task ScheduleScanForManagedFolder(IManagedFolder folder, bool onlyNewFiles = false, bool skipMylist = false, bool prioritize = true)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<ScanFolderJob>(j => (j.ManagedFolderID, j.OnlyNewFiles, j.SkipMyList) = (folder.ID, onlyNewFiles, skipMylist), prioritize);
    }

    public async Task ScheduleScanForManagedFolders(bool onlyDropSources = false, bool? onlyNewFiles = null, bool skipMylist = false, bool prioritize = true)
    {
        var managedFolders = _managedFolderRepository.GetAll();
        var sources = managedFolders.Where(a => a.DropFolderType.HasFlag(DropFolderType.Source)).ToList();
        var rest = onlyDropSources ? [] : managedFolders.Except(sources).ToList();
        if (!onlyNewFiles.HasValue)
        {
            foreach (var source in sources)
                await ScheduleScanForManagedFolder(source, prioritize: true);
            foreach (var folder in rest)
                await ScheduleScanForManagedFolder(folder, onlyNewFiles: true, prioritize: true);
            return;
        }

        foreach (var source in sources)
            await ScheduleScanForManagedFolder(source, prioritize: true);
        foreach (var folder in rest)
            await ScheduleScanForManagedFolder(folder, onlyNewFiles: onlyNewFiles.Value, prioritize: true);
    }

    #endregion Managed Folder
}
