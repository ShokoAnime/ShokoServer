using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MimeDetective;
using MimeDetective.Definitions.Licensing;
using MimeDetective.Storage;
using Namotion.Reflection;
using Polly;
using Quartz;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Events;
using Shoko.Abstractions.Hashing;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Services;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video;
using Shoko.Server.Hashing;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Plugin;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Services;

public class VideoHashingService(
    ILogger<VideoHashingService> logger,
    IServiceProvider serviceProvider,
    ISchedulerFactory schedulerFactory,
    IConfigurationService configurationService,
    IVideoReleaseService videoReleaseService,
    IPluginManager pluginManager,
    ISettingsProvider settingsProvider,
    IRelocationService relocationService,
    ConfigurationProvider<VideoHashingServiceSettings> configurationProvider,
    ShokoManagedFolderRepository managedFolderRepository,
    VideoLocalRepository videoRepository,
    VideoLocal_PlaceRepository locationRepository,
    VideoLocal_HashDigestRepository hashesRepository,
    FileNameHashRepository fileNameHashRepository
) : IVideoHashingService
{
    private VideoService? __videoService;

    private VideoService _videoService => __videoService ??= (VideoService)serviceProvider.GetRequiredService<IVideoService>();

    private const string ED2K = "ED2K";

    private IContentInspector? _contentInspector;

    private Dictionary<Guid, HashProviderInfo> _hashProviderInfos = [];

    private readonly object _lock = new();

    private bool _loaded = false;

    private Guid _coreProviderID = Guid.Empty;

    public event EventHandler<FileHashedEventArgs>? FileHashed;

    public event EventHandler? ProvidersUpdated;

    public bool ParallelMode
    {
        get => configurationProvider.Load().ParallelMode;
        set
        {
            var config = configurationProvider.Load();
            if (config.ParallelMode == value)
                return;

            config.ParallelMode = value;
            configurationProvider.Save(config);
            ProvidersUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    public IReadOnlySet<string> AllAvailableHashTypes => GetAvailableProviders()
        .SelectMany(p => p.Provider.AvailableHashTypes)
        .ToHashSet();

    public IReadOnlySet<string> AllEnabledHashTypes => GetAvailableProviders(onlyEnabled: true)
        .SelectMany(p => p.EnabledHashTypes)
        .ToHashSet();

    #region Add Parts

    public void AddParts(IEnumerable<IHashProvider> providers)
    {
        if (_loaded) return;
        lock (_lock)
        {
            if (_loaded) return;

            logger.LogInformation("Initializing providers.");
            var config = configurationProvider.Load();
            var enabled = config.EnabledHashes;
            _coreProviderID = GetID(typeof(CoreHashProvider), pluginManager.GetPluginInfo<CorePlugin>()!);
            _hashProviderInfos = providers
                .Select((provider, priority) =>
                {
                    var providerType = provider.GetType();
                    var pluginInfo = pluginManager.GetPluginInfo(providerType.Assembly)!;
                    var id = GetID(providerType, pluginInfo);
                    var contextualType = providerType.ToContextualType();
                    var enabledHashes = enabled.Where(kp => kp.Value == id).Select(kp => kp.Key).Order().ToHashSet();
                    var description = provider.Description?.CleanDescription() ?? string.Empty;
                    var configurationType = providerType.GetInterfaces()
                        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHashProvider<>))
                        ?.GetGenericArguments()[0];
                    var configurationInfo = configurationType is null ? null : configurationService.GetConfigurationInfo(configurationType);
                    return new HashProviderInfo()
                    {
                        ID = id,
                        Version = provider.Version,
                        Name = provider.Name,
                        Description = description,
                        Provider = provider,
                        ConfigurationInfo = configurationInfo,
                        PluginInfo = pluginInfo,
                        EnabledHashTypes = enabledHashes,
                    };
                })
                .OrderByDescending(info => typeof(CorePlugin) == info.PluginInfo.PluginType)
                .ThenBy(info => info.PluginInfo.Name)
                .ThenBy(info => info.Name)
                .ThenBy(info => info.ID)
                .ToDictionary(info => info.ID);

            logger.LogInformation("Building content inspector.");
            _contentInspector = new ContentInspectorBuilder()
            {
                Definitions = new MimeDetective.Definitions.CondensedBuilder() { UsageType = UsageType.PersonalNonCommercial, }
                    .Build()
                    .ScopeExtensions(["3g2", "3gp", "avi", "flv", "h264", "m4v", "mkv", "mov", "mp4", "mpg", "mpeg", "ogv", "ogg", "qt", "rm", "swf", "vob", "wmv", "webm"])
                    .TrimMeta()
                    .TrimDescription()
                    .TrimMimeType()
                    .TrimCategories()
                    .ToImmutableArray(),
            }.Build();

            _loaded = true;
        }

        UpdateProviders(false);

        logger.LogInformation("Loaded {ProviderCount} providers.", _hashProviderInfos.Count);
    }

    #endregion

    #region Providers

    public IEnumerable<HashProviderInfo> GetAvailableProviders(bool onlyEnabled = false)
        => _hashProviderInfos.Values
            .Where(info => !onlyEnabled || info.EnabledHashTypes.Count > 0)
            .OrderByDescending(info => typeof(CorePlugin) == info.PluginInfo.PluginType)
            .ThenBy(info => info.PluginInfo.Name)
            .ThenBy(info => info.Name)
            .ThenBy(info => info.ID)
            // Create a copy so that we don't affect the original entries
            .Select(info => new HashProviderInfo()
            {
                ID = info.ID,
                Version = info.Version,
                Name = info.Name,
                Description = info.Description,
                Provider = info.Provider,
                ConfigurationInfo = info.ConfigurationInfo,
                PluginInfo = info.PluginInfo,
                EnabledHashTypes = info.EnabledHashTypes.ToHashSet(),
            });

    public IReadOnlyList<HashProviderInfo> GetProviderInfo(IPlugin plugin)
        => _hashProviderInfos.Values
            .Where(info => info.PluginInfo.ID == plugin.ID)
            .OrderBy(info => info.Name)
            .ThenBy(info => info.ID)
            // Create a copy so that we don't affect the original entries
            .Select(info => new HashProviderInfo()
            {
                ID = info.ID,
                Version = info.Version,
                Name = info.Name,
                Description = info.Description,
                Provider = info.Provider,
                ConfigurationInfo = info.ConfigurationInfo,
                PluginInfo = info.PluginInfo,
                EnabledHashTypes = info.EnabledHashTypes.ToHashSet(),
            })
            .ToList();

    public HashProviderInfo GetProviderInfo(IHashProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        EnsureLoaded();

        return GetProviderInfo(GetID(provider.GetType()))
            ?? throw new ArgumentException($"Unregistered provider: '{provider.GetType().Name}'", nameof(provider));
    }

    public HashProviderInfo GetProviderInfo<TProvider>() where TProvider : class, IHashProvider
    {
        EnsureLoaded();

        return GetProviderInfo(GetID(typeof(TProvider)))
            ?? throw new ArgumentException($"Unregistered provider: '{typeof(TProvider).Name}'", nameof(TProvider));
    }

    public HashProviderInfo? GetProviderInfo(Guid providerID)
        => _hashProviderInfos?.TryGetValue(providerID, out var providerInfo) ?? false
            // Create a copy so that we don't affect the original entry
            ? new()
            {
                ID = providerInfo.ID,
                Version = providerInfo.Version,
                Name = providerInfo.Name,
                Description = providerInfo.Description,
                Provider = providerInfo.Provider,
                ConfigurationInfo = providerInfo.ConfigurationInfo,
                PluginInfo = providerInfo.PluginInfo,
                EnabledHashTypes = providerInfo.EnabledHashTypes.ToHashSet(),
            }
            : null;

    public void UpdateProviders(params HashProviderInfo[] providers)
        => UpdateProviders(true, providers);

    private void UpdateProviders(bool fireEvent, params HashProviderInfo[] providers)
    {
        EnsureLoaded();

        var existingProviders = GetAvailableProviders().ToList();
        foreach (var providerInfo in providers)
        {
            if (existingProviders.Find(p => p.Provider == providerInfo.Provider) is not { } existingProvider)
                continue;

            // Enable or disable provider.
            providerInfo.EnabledHashTypes.IntersectWith(providerInfo.Provider.AvailableHashTypes);
            if (!providerInfo.EnabledHashTypes.SetEquals(existingProvider.EnabledHashTypes))
                existingProvider.EnabledHashTypes = providerInfo.EnabledHashTypes;
        }

        var changed = false;
        var config = configurationProvider.Load();

        // Remove any providers with no hashes.
        var enabled = existingProviders
            .Where(p => p.EnabledHashTypes.Count > 0)
            .SelectMany(p => p.EnabledHashTypes.Select(h => (p.ID, Value: h)))
            .DistinctBy(kp => kp.Value)
            .OrderBy(kp => kp.Value)
            .ToDictionary(kp => kp.Value, kp => kp.ID);
        // Ensure we at have an ED2K hash provider at all times.
        enabled.TryAdd(ED2K, _coreProviderID);

        if (!config.EnabledHashes.SequenceEqual(enabled))
        {
            config.EnabledHashes = enabled;
            changed = true;
        }

        if (changed)
        {
            lock (_lock)
            {
                _hashProviderInfos = existingProviders
                    // Create a copy so that we don't affect the original entry
                    .Select(info => new HashProviderInfo()
                    {
                        ID = info.ID,
                        Version = info.Version,
                        Name = info.Name,
                        Description = info.Description,
                        Provider = info.Provider,
                        ConfigurationInfo = info.ConfigurationInfo,
                        PluginInfo = info.PluginInfo,
                        EnabledHashTypes = enabled.Where(kp => kp.Value == info.ID).Select(kp => kp.Key).Order().ToHashSet(),
                    })
                    .ToDictionary(info => info.ID);
            }
            configurationProvider.Save(config);
            if (fireEvent)
                ProvidersUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    #endregion

    #region Get Hashes

    public async Task<HashingResult> GetHashesForPath(string path, bool useExistingHashes = true, bool skipFindRelease = false, bool skipMylist = false, CancellationToken cancellationToken = default)
    {
        EnsureLoaded();
        path = PlatformUtility.EnsureUsablePath(path);
        if (!File.Exists(path))
            throw new FileNotFoundException($"File does not exist: {path}", path);

        var resolvedPath = File.ResolveLinkTarget(path, true)?.FullName;
        if (!string.IsNullOrEmpty(resolvedPath))
        {
            logger.LogTrace("File is a symbolic link. Resolved path: {ResolvedFilePath}", resolvedPath);
            if (!File.Exists(resolvedPath))
                throw new FileNotFoundException($"Symbolic link points to file that does not exist: {resolvedPath}", resolvedPath);
        }

        var (managedFolder, relativePath) = managedFolderRepository.GetFromAbsolutePath(path);
        if (managedFolder is null || string.IsNullOrEmpty(relativePath))
            throw new InvalidOperationException($"File is outside of any managed folders: {path}");

        VideoLocal? video = null;
        var videoLocation = locationRepository.GetByRelativePathAndManagedFolderID(relativePath, managedFolder.ID);
        if (videoLocation is not null)
        {
            video = videoLocation.VideoLocal;
            if (video is not null)
            {
                logger.LogTrace("VideoLocal record found in database: {Filename} (ManagedFolder={ManagedFolderID})", relativePath, managedFolder.ID);

                // This will only happen with DB corruption, so just clean up the mess.
                if (videoLocation.Path is null)
                {
                    if (video.Places.Count == 1)
                    {
                        videoRepository.Delete(video);
                        video = null;
                    }

                    locationRepository.Delete(videoLocation);
                    videoLocation = null;
                }
            }
        }
        if (video is null)
        {
            logger.LogTrace("No existing VideoLocal, using temporary record");
            var now = DateTime.Now;
            video = new VideoLocal
            {
                DateTimeUpdated = now,
                DateTimeCreated = now,
#pragma warning disable CS0618 // Type or member is obsolete
                FileName = Path.GetFileName(relativePath),
#pragma warning restore CS0618 // Type or member is obsolete
            };
        }

        if (videoLocation is null)
        {
            logger.LogTrace("No existing VideoLocal_Place, using temporary record");
            videoLocation = new VideoLocal_Place
            {
                RelativePath = relativePath,
                ManagedFolderID = managedFolder.ID,
            };
            if (video.VideoLocalID != 0)
                videoLocation.VideoID = video.VideoLocalID;
        }

        return await GetHashesForVideo(video, videoLocation, managedFolder, useExistingHashes, skipFindRelease, skipMylist, cancellationToken).ConfigureAwait(false);
    }

    public async Task ScheduleGetHashesForPath(string path, bool useExistingHashes = true, bool skipFindRelease = false, bool skipMylist = false, bool prioritize = false)
    {
        EnsureLoaded();
        path = PlatformUtility.EnsureUsablePath(path);
        if (!File.Exists(path))
            throw new FileNotFoundException($"File does not exist: {path}", path);

        var resolvedPath = File.ResolveLinkTarget(path, true)?.FullName;
        if (string.IsNullOrEmpty(resolvedPath))
        {
            resolvedPath = path;
        }
        else
        {
            logger.LogTrace("File is a symbolic link. Resolved path: {ResolvedFilePath}", resolvedPath);
            if (!File.Exists(resolvedPath))
                throw new FileNotFoundException($"Symbolic link points to file that does not exist: {resolvedPath}", resolvedPath);
        }

        var (managedFolder, relativePath) = managedFolderRepository.GetFromAbsolutePath(path);
        if (managedFolder is null || string.IsNullOrEmpty(relativePath))
            throw new InvalidOperationException($"File is outside of any managed folders: {path}");

        // Verify that the _unknown_ file we're going to hash is, in fact, a video file.
        if (locationRepository.GetByRelativePathAndManagedFolderID(relativePath, managedFolder.ID) is null && !IsVideoFile(resolvedPath))
            throw new InvalidOperationException($"File is not a known video file format: {resolvedPath}");

        var scheduler = await schedulerFactory.GetScheduler();
        await scheduler.StartJob<HashFileJob>(b => (b.FilePath, b.ForceHash, b.SkipFindRelease, b.SkipMyList) = (path, !useExistingHashes, skipFindRelease, skipMylist), prioritize: prioritize);
    }

    public async Task<HashingResult> GetHashesForFile(IVideoFile file, bool useExistingHashes = true, bool skipFindRelease = false, bool skipMylist = false, CancellationToken cancellationToken = default)
    {
        EnsureLoaded();
        var path = PlatformUtility.EnsureUsablePath(file.Path);
        if (!File.Exists(path))
            throw new FileNotFoundException($"File does not exist: {path}", path);

        var resolvedPath = File.ResolveLinkTarget(path, true)?.FullName;
        if (!string.IsNullOrEmpty(resolvedPath))
        {
            logger.LogTrace("File is a symbolic link. Resolved path: {ResolvedFilePath}", resolvedPath);
            if (!File.Exists(resolvedPath))
                throw new FileNotFoundException($"Symbolic link points to file that does not exist: {resolvedPath}", resolvedPath);
        }

        var videoLocation = locationRepository.GetByID(file.ID)
            ?? throw new InvalidOperationException($"No VideoLocal_Place record found for ID: {file.ID}");
        var video = videoLocation.VideoLocal
            ?? throw new InvalidOperationException($"No VideoLocal record found for ID: {videoLocation.VideoID}");
        var managedFolder = videoLocation.ManagedFolder
            ?? throw new InvalidOperationException($"No ManagedFolder record found for ID: {videoLocation.ManagedFolderID}");
        return await GetHashesForVideo(video, videoLocation, managedFolder, useExistingHashes, skipFindRelease, skipMylist, cancellationToken).ConfigureAwait(false);
    }

    public async Task ScheduleGetHashesForFile(IVideoFile file, bool useExistingHashes = true, bool skipFindRelease = false, bool skipMylist = false, bool prioritize = false)
    {
        EnsureLoaded();
        var path = PlatformUtility.EnsureUsablePath(file.Path);
        if (!File.Exists(path))
            throw new FileNotFoundException($"File does not exist: {path}", path);

        var resolvedPath = File.ResolveLinkTarget(path, true)?.FullName;
        if (!string.IsNullOrEmpty(resolvedPath))
        {
            logger.LogTrace("File is a symbolic link. Resolved path: {ResolvedFilePath}", resolvedPath);
            if (!File.Exists(resolvedPath))
                throw new FileNotFoundException($"Symbolic link points to file that does not exist: {resolvedPath}", resolvedPath);
        }

        var scheduler = await schedulerFactory.GetScheduler();
        await scheduler.StartJob<HashFileJob>(b => (b.FilePath, b.ForceHash, b.SkipFindRelease, b.SkipMyList) = (path, !useExistingHashes, skipFindRelease, skipMylist), prioritize: prioritize);
    }

    #region Internals

    private void EnsureLoaded()
    {
        if (!_loaded)
            throw new InvalidOperationException("Providers have not been added yet.");
    }

    private bool IsVideoFile(string path)
        => _contentInspector!.Inspect(path, ContentReader.Min).Length > 0;

    private async Task<HashingResult> GetHashesForVideo(VideoLocal video, VideoLocal_Place videoLocation, ShokoManagedFolder folder, bool useExistingHashes, bool skipFindRelease = false, bool skipMylist = false, CancellationToken cancellationToken = default)
    {
        var originalPath = PlatformUtility.EnsureUsablePath(Path.Join(folder.Path, videoLocation.RelativePath));
        var resolvedPath = File.ResolveLinkTarget(originalPath, true)?.FullName;
        if (string.IsNullOrEmpty(resolvedPath))
            resolvedPath = originalPath;
        else
            logger.LogTrace("File is a symbolic link. Resolved path: {ResolvedFilePath}", resolvedPath);

        if (!TryGetFileSize(originalPath, resolvedPath, out var fileSize, out var e))
            throw new UnauthorizedAccessException($"Could not access file to read file size or file size is 0: {resolvedPath}", e);

        // Verify that the _unknown_ file we're going to hash is, in fact, a video file.
        if (!IsVideoFile(resolvedPath))
            throw new InvalidOperationException($"File is not a known video file format: {resolvedPath}");

        var existingHashes = !useExistingHashes ? [] : video.Hashes;
        var hashes = await GetHashesForFile(new FileInfo(resolvedPath), existingHashes, cancellationToken).ConfigureAwait(false);
        var ed2k = hashes.FirstOrDefault(x => x.Type is ED2K);
        if (ed2k is not { Type: ED2K, Value.Length: 32 })
            throw new InvalidOperationException($"Could not get ED2K hash for {originalPath}");

        if (videoRepository.GetByEd2k(ed2k.Value) is { } otherVideo && video.VideoLocalID != otherVideo.VideoLocalID)
        {
            logger.LogTrace("Found existing video with ED2K hash {ED2K}: {VideoID}→{OtherVideoID}", ed2k.Value, video.VideoLocalID, otherVideo.VideoLocalID);
            video = otherVideo;
        }

        // Store the hashes
        var isNewVideo = video.VideoLocalID is 0;
        var isNewFile = video.FileSize is 0;
        logger.LogTrace("Saving Video: Filename: {FileName}, Hash: {Hash}", originalPath, video.Hash);
        video.Hash = ed2k.Value;
        video.FileSize = fileSize;
        video.DateTimeUpdated = DateTime.Now;
        videoRepository.Save(video, false);

        // Save the hashes
        var newHashes = hashes
            .Select(x => new VideoLocal_HashDigest()
            {
                VideoLocalID = video.VideoLocalID,
                Type = x.Type,
                Value = x.Value,
                Metadata = x.Metadata,
            })
            .ToList();

        // Re-fetch the hashes in case we changed to an existing video.
        existingHashes = video.Hashes;

        var toRemove = existingHashes.Except(newHashes).ToList();
        var toSave = newHashes.Except(existingHashes).ToList();
        hashesRepository.Save(toSave);
        hashesRepository.Delete(toRemove);

        videoLocation.VideoID = video.VideoLocalID;
        locationRepository.Save(videoLocation);

        var wasDeleted = await DeduplicateAndCleanup(video, videoLocation, updateMyList: !skipMylist, locationAvailable: true).ConfigureAwait(false);
        if (!wasDeleted)
        {
            SaveFileNameHash(videoLocation.FileName, video);

            if ((video.MediaInfo?.GeneralStream?.Duration ?? 0) == 0 || video.MediaVersion < VideoLocal.MEDIA_VERSION)
            {
                if (_videoService.RefreshMediaInfo(videoLocation, video))
                    videoRepository.Save(video, false);
            }
        }

        try
        {
            FileHashed?.Invoke(null, new(videoLocation.RelativePath, folder, videoLocation, video)
            {
                IsNewFile = isNewFile,
                IsNewVideo = isNewVideo,
                UsedExistingHashes = useExistingHashes,
                Hashes = hashes,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred in 'FileHashed' event");
        }

        var settings = settingsProvider.GetSettings();
        var shouldRelocate = !isNewVideo && video.ReleaseInfo is not null && !useExistingHashes && settings.Plugins.Renamer.RelocateOnImport && (
            folder.DropFolderType.HasFlag(DropFolderType.Source) ||
            (folder.DropFolderType.HasFlag(DropFolderType.Destination) && settings.Plugins.Renamer.AllowRelocationInsideDestinationOnImport)
        );
        if (shouldRelocate)
            await relocationService.ScheduleAutoRelocationForVideoFile(videoLocation).ConfigureAwait(false);

        // Add the process file job if we're not forcefully re-hashing the file.
        if (!skipFindRelease && (useExistingHashes || video.ReleaseInfo is null))
            await videoReleaseService.ScheduleFindReleaseForVideo(video, force: !useExistingHashes, addToMylist: !skipMylist).ConfigureAwait(false);

        return new()
        {
            IsNewFile = isNewFile,
            IsNewVideo = isNewVideo,
            UsedExistingHashes = useExistingHashes,
            Video = video,
            File = videoLocation,
            Hashes = hashes,
        };
    }

    #region Get File Size

    internal bool TryGetFileSize(string originalPath, string resolvedPath, out long size, [NotNullWhen(false)] out Exception? e)
    {
        size = 0;
        var settings = settingsProvider.GetSettings();
        if (settings.Import.FileLockChecking)
        {
            var waitTime = settings.Import.FileLockWaitTimeMS;
            var policy = Policy
                .HandleResult<long>(result => result == 0)
                .Or<IOException>()
                .Or<UnauthorizedAccessException>(ex => HandleReadOnlyException(ex, resolvedPath, !string.Equals(originalPath, resolvedPath, StringComparison.Ordinal)))
                .Or<Exception>(ex =>
                {
                    logger.LogError(ex, "Could not access file: {Filename}", resolvedPath);
                    return false;
                })
                .WaitAndRetry(60, _ => TimeSpan.FromMilliseconds(waitTime), (_, _, count, _) =>
                {
                    logger.LogTrace("Failed to access, (or filesize is 0) Attempt # {NumAttempts}, {FileName}", count, resolvedPath);
                });

            var result = policy.ExecuteAndCapture(() => GetFileSize(resolvedPath, FileAccess.Read));
            if (result.Outcome == OutcomeType.Failure)
            {
                if (result.FinalException is not null)
                {
                    logger.LogError(result.FinalException, "Could not access file: {Filename}", resolvedPath);
                    e = result.FinalException;
                }
                else
                {
                    logger.LogError("Could not access file: {Filename}", resolvedPath);
                    e = new InvalidOperationException($"Could not access file: {resolvedPath}");
                }
                return false;
            }
        }

        if (File.Exists(resolvedPath))
        {
            try
            {
                size = GetFileSize(resolvedPath, FileAccess.Read);
                e = null;
                return size > 0;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not access file: {Filename}", resolvedPath);
                e = exception;
                return false;
            }
        }

        logger.LogError("Could not access file: {Filename}", resolvedPath);
        e = new InvalidOperationException($"Could not access file: {resolvedPath}");
        return false;
    }

    private static long GetFileSize(string fileName, FileAccess accessType)
    {
        using var fs = File.Open(fileName, FileMode.Open, accessType, FileShare.ReadWrite);
        var size = fs.Seek(0, SeekOrigin.End);
        return size;
    }

    private bool HandleReadOnlyException(Exception ex, string path, bool isSymbolicLink)
    {
        // If it's a symbolic link or we're running on linux or mac then abort now.
        if (isSymbolicLink || !PlatformUtility.IsWindows)
        {
#pragma warning disable CA2254 // Template should be a static expression
            logger.LogError(ex, $"Failed to read read-only {(isSymbolicLink ? "symbolic link target" : "file")}: {{Filename}}", path);
#pragma warning restore CA2254 // Template should be a static expression
            return false;
        }

        logger.LogTrace("File {FileName} is Read-Only, attempting to unmark", path);
        try
        {
            var info = new FileInfo(path);
            if (info.IsReadOnly) info.IsReadOnly = false;
            if (!info.IsReadOnly)
                return true;
        }
        catch
        {
            // ignore, we tried
        }

        return false;
    }

    #endregion

    #region Get Hashes for FileInfo

    public async Task<IReadOnlyList<IHashDigest>> GetHashesForFile(FileInfo fileInfo, IReadOnlyList<IHashDigest>? existingHashes = null, CancellationToken cancellationToken = default)
    {
        if (!_loaded)
            return [];

        existingHashes ??= [];
        var providers = GetAvailableProviders(onlyEnabled: true).ToList();
        return ParallelMode ?
            await GetHashesForFileParallel(fileInfo, existingHashes, providers, cancellationToken) :
            await GetHashesForFileSequential(fileInfo, existingHashes, providers, cancellationToken);
    }

    private static async Task<IReadOnlyList<IHashDigest>> GetHashesForFileParallel(FileInfo fileInfo, IReadOnlyList<IHashDigest> existingHashes, IList<HashProviderInfo> providers, CancellationToken cancellationToken)
    {
        var allHashes = new ConcurrentBag<(HashProviderInfo Provider, IReadOnlyList<IHashDigest> Hashes)>() { };
        await Task.WhenAll(providers.Select(providerInfo => Task.Run(async () =>
        {
            var newHashes = await GetHashesForFileAndProvider(fileInfo, providerInfo, existingHashes, cancellationToken);
            allHashes.Add((providerInfo, newHashes));
        }, cancellationToken)));
        return allHashes.ToArray()
            .OrderBy(tuple => providers.IndexOf(tuple.Provider))
            .Select(tuple => tuple.Hashes)
            .Prepend(existingHashes)
            .SelectMany(h => h)
            .Distinct()
            .Order()
            .ToList();
    }

    private static async Task<IReadOnlyList<IHashDigest>> GetHashesForFileSequential(FileInfo fileInfo, IReadOnlyList<IHashDigest> existingHashes, IReadOnlyList<HashProviderInfo> providers, CancellationToken cancellationToken)
    {
        var allHashes = new List<IHashDigest>(existingHashes);
        foreach (var providerInfo in providers)
        {
            var newHashes = await GetHashesForFileAndProvider(fileInfo, providerInfo, existingHashes, cancellationToken);
            allHashes.AddRange(newHashes);
            if (cancellationToken.IsCancellationRequested)
                break;
        }

        return allHashes;
    }

    private static async Task<IReadOnlyList<IHashDigest>> GetHashesForFileAndProvider(FileInfo fileInfo, HashProviderInfo providerInfo, IReadOnlyList<IHashDigest> existingHashes, CancellationToken cancellationToken)
    {
        var enabledHashTypes = providerInfo.EnabledHashTypes;
        var request = new HashingRequest()
        {
            EnabledHashTypes = enabledHashTypes,
            ExistingHashes = existingHashes.Where(h => enabledHashTypes.Contains(h.Type)).ToList(),
            File = fileInfo,
        };
        var hashes = await providerInfo.Provider.GetHashesForVideo(request, cancellationToken);
        return hashes
            .Where(h => enabledHashTypes.Contains(h.Type))
            .ToList();
    }

    #endregion

    #region De-duplicate & Cleanup

    internal async Task<bool> DeduplicateAndCleanup(VideoLocal video, VideoLocal_Place videoLocation, bool updateMyList, bool locationAvailable)
    {
        if (video.VideoLocalID is 0) return false;

        var toRemove = video.Places
            .Where(a => videoLocation.ID == a.ID ? !locationAvailable : !a.IsAvailable)
            .ToList();
        foreach (var vlp in toRemove)
            await _videoService.RemoveRecord(vlp, updateMyListStatus: updateMyList);

        var places = video.Places;
        if (videoLocation.ID is not 0 && places is { Count: 0 })
        {
            logger.LogWarning("Removed record for unavailable location: {Path}", videoLocation.Path);
            return true;
        }

        if (places.FirstOrDefault(a => videoLocation.ID != a.ID) is not { } dupPlace)
            return false;

        logger.LogWarning("---------------------------------------------");
        logger.LogWarning("Found Duplicate File");
        logger.LogWarning("---------------------------------------------");
        logger.LogWarning("New File: {FullServerPath}", videoLocation.Path);
        logger.LogWarning("Existing File: {FullServerPath}", dupPlace.Path);
        logger.LogWarning("---------------------------------------------");

        var settings = settingsProvider.GetSettings();
        if (!settings.Import.AutomaticallyDeleteDuplicatesOnImport)
            return false;

        logger.LogInformation("Auto De-Duplicating Is Enabled. Deleting Duplicate File: {FullServerPath}", dupPlace.Path);
        logger.LogWarning("---------------------------------------------");
        await _videoService.RemoveRecordAndDeletePhysicalFile(videoLocation, updateMyList: updateMyList);
        return true;
    }

    #endregion

    #region Save File Name Hash

    private void SaveFileNameHash(string filename, VideoLocal vlocal)
    {
        // also save the filename to hash record
        // replace the existing records just in case it was corrupt
        var hashes = fileNameHashRepository.GetByFileNameAndSize(filename, vlocal.FileSize);
        if (hashes is { Count: > 1 })
        {
            // if we have more than one record it probably means there is some sort of corruption
            // lets delete the local records
            fileNameHashRepository.Delete(hashes);
        }

        var hash = hashes is { Count: 1 } ? hashes[0] : new();
        hash.FileName = filename;
        hash.FileSize = vlocal.FileSize;
        hash.Hash = vlocal.Hash;
        hash.DateTimeUpdated = DateTime.Now;
        fileNameHashRepository.Save(hash);
    }

    #endregion

    #endregion

    #endregion

    #region ID Helpers

    private Guid GetID(Type providerType)
        => _loaded && pluginManager.GetPluginInfo(providerType.Assembly) is { } pluginInfo
            ? GetID(providerType, pluginInfo)
            : Guid.Empty;

    private static Guid GetID(Type type, PluginInfo pluginInfo)
        => UuidUtility.GetV5($"HashProvider={type.FullName!}", pluginInfo.ID);

    #endregion
}
