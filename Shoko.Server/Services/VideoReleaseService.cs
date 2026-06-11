using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.User.Services;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Events;
using Shoko.Abstractions.Video.Release;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Plugin;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.Release;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Scheduling.Jobs.TMDB;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Services;

public class VideoReleaseService(
    ILogger<VideoReleaseService> logger,
    IConfigurationService configurationService,
    IUDPConnectionHandler udpConnection,
    ISettingsProvider settingsProvider,
    ConfigurationProvider<VideoReleaseServiceSettings> configurationProvider,
    IQueueScheduler schedulerFactory,
    QueueJobTypeRegistry jobTypeRegistry,
    IRequestFactory requestFactory,
    IUserService userService,
    IServiceProvider serviceProvider,
    IPluginManager pluginManager,
    IVideoRelocationService relocationService,
    VideoLocalRepository videoRepository,
    StoredReleaseInfoRepository releaseInfoRepository,
    StoredReleaseInfo_MatchAttemptRepository releaseInfoMatchAttemptRepository,
    AniDB_EpisodeRepository anidbEpisodeRepository,
    AniDB_AnimeRepository anidbAnimeRepository,
    AniDB_AnimeUpdateRepository anidbAnimeUpdateRepository,
    AnimeSeriesRepository shokoSeriesRepository,
    CrossRef_AniDB_TMDB_ShowRepository crossRefAnidbTmdbRepository,
    CrossRef_File_EpisodeRepository xrefRepository
) : IVideoReleaseService
{
    // We need to lazy init. the user data service since otherwise there will be
    // a circular dependency between this service, the user data service, and
    // the anime series service (which in turn depend on this service). So we
    // lazy init the user data service to break the circle.
    private IUserDataService? _userDataService;

    // Lazy init. to prevent circular dependency.
    private IAnidbService? _anidbService;

    private IServerSettings _settings => settingsProvider.GetSettings();

    private Dictionary<Guid, ReleaseProviderInfo> _releaseProviderInfos = [];

    // Maps IReleaseInfoProvider concrete type → job type (from IVideoReleaseProviderJob<T> implementations)
    private Dictionary<Type, Type> _providerJobTypes = [];

    private readonly HashSet<int> _unknownEpisodeIDs = [];

    private readonly Lock _lock = new();

    private bool _loaded;

    public bool AutoMatchEnabled { get; private set; }

    public event EventHandler<VideoReleaseSavedEventArgs>? ReleaseSaved;

    public event EventHandler<VideoReleaseDeletedEventArgs>? ReleaseDeleted;

    public event EventHandler<VideoReleaseSearchCompletedEventArgs>? SearchCompleted;

    public event EventHandler? ProvidersUpdated;

    #region Add Parts

    public void AddParts(IEnumerable<IReleaseInfoProvider> providers)
    {
        if (_loaded) return;
        _loaded = true;

        logger.LogInformation("Initializing service.");

        lock (_lock)
        {
            var config = configurationProvider.Load();
            var order = config.Priority;
            var enabled = config.Enabled;
            _releaseProviderInfos = providers
                .Select(provider =>
                {
                    var providerType = provider.GetType();
                    var pluginInfo = pluginManager.GetPluginInfo(providerType.Assembly)!;
                    var id = GetID(providerType, pluginInfo);
                    var isEnabled = enabled.TryGetValue(id, out var enabledValue) ? enabledValue : provider.Name is "AniDB";
                    var description = provider.Description?.CleanDescription() ?? string.Empty;
                    var configurationType = providerType.GetInterfaces()
                        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReleaseInfoProvider<>))
                        ?.GetGenericArguments()[0];
                    var configurationInfo = configurationType is null ? null : configurationService.GetConfigurationInfo(configurationType);
                    return new ReleaseProviderInfo()
                    {
                        ID = id,
                        Version = provider.Version,
                        Name = provider.Name,
                        Description = description,
                        Provider = provider,
                        ConfigurationInfo = configurationInfo,
                        PluginInfo = pluginInfo,
                        Enabled = isEnabled,
                        Priority = -1,
                    };
                })
                .OrderBy(p => order.IndexOf(p.ID) is -1)
                .ThenBy(p => order.IndexOf(p.ID))
                .ThenBy(p => p.ID)
                .Select((info, priority) => new ReleaseProviderInfo()
                {
                    ID = info.ID,
                    Version = info.Version,
                    Name = info.Name,
                    Description = info.Description,
                    Provider = info.Provider,
                    ConfigurationInfo = info.ConfigurationInfo,
                    PluginInfo = info.PluginInfo,
                    Enabled = info.Enabled,
                    Priority = priority,
                })
                .ToDictionary(info => info.ID);
            AutoMatchEnabled = _releaseProviderInfos.Values.Any(p => p.Enabled);
        }

        UpdateProviders(false);

        // Build the provider type → job type map from IVideoReleaseProviderJob<T> implementations
        _providerJobTypes = jobTypeRegistry.JobTypes
            .SelectMany(jobType => jobType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IVideoReleaseProviderJob<>))
                .Select(i => (providerType: i.GetGenericArguments()[0], jobType)))
            .ToDictionary(t => t.providerType, t => t.jobType);

        logger.LogInformation("Loaded {ProviderCount} providers, {JobCount} provider job mappings.",
            _releaseProviderInfos.Count, _providerJobTypes.Count);
    }

    #endregion Add Parts

    #region Provider Info

    public IEnumerable<ReleaseProviderInfo> GetAvailableProviders(bool onlyEnabled = false)
        => _releaseProviderInfos.Values
            .Where(info => !onlyEnabled || info.Enabled)
            .OrderBy(info => info.Priority)
            // Create a copy so that we don't affect the original entries
            .Select(info => new ReleaseProviderInfo()
            {
                ID = info.ID,
                Version = info.Version,
                Name = info.Name,
                Description = info.Description,
                Provider = info.Provider,
                ConfigurationInfo = info.ConfigurationInfo,
                PluginInfo = info.PluginInfo,
                Enabled = info.Enabled,
                Priority = info.Priority,
            });

    public IReadOnlyList<ReleaseProviderInfo> GetProviderInfo(IPlugin plugin)
        => _releaseProviderInfos.Values
            .Where(info => info.PluginInfo.ID == plugin.ID)
            .OrderBy(info => info.Name)
            .ThenBy(info => info.ID)
            // Create a copy so that we don't affect the original entries
            .Select(info => new ReleaseProviderInfo()
            {
                ID = info.ID,
                Version = info.Version,
                Name = info.Name,
                Description = info.Description,
                Provider = info.Provider,
                ConfigurationInfo = info.ConfigurationInfo,
                PluginInfo = info.PluginInfo,
                Enabled = info.Enabled,
                Priority = info.Priority,
            })
            .ToList();

    public ReleaseProviderInfo GetProviderInfo(IReleaseInfoProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (!_loaded)
            throw new InvalidOperationException("Providers have not been added yet.");

        return GetProviderInfo(GetID(provider.GetType()))
            ?? throw new ArgumentException($"Unregistered provider: '{provider.GetType().Name}'", nameof(provider));
    }

    public ReleaseProviderInfo GetProviderInfo<TProvider>() where TProvider : class, IReleaseInfoProvider
    {
        if (!_loaded)
            throw new InvalidOperationException("Providers have not been added yet.");

        return GetProviderInfo(GetID(typeof(TProvider)))
            ?? throw new ArgumentException($"Unregistered provider: '{typeof(TProvider).Name}'", nameof(TProvider));
    }

    public ReleaseProviderInfo? GetProviderInfo(Guid providerID)
        => _releaseProviderInfos?.TryGetValue(providerID, out var providerInfo) ?? false
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
                Enabled = providerInfo.Enabled,
                Priority = providerInfo.Priority,
            }
            : null;

    public void UpdateProviders(params ReleaseProviderInfo[] providers)
        => UpdateProviders(true, providers);

    private void UpdateProviders(bool fireEvent, params ReleaseProviderInfo[] providers)
    {
        if (!_loaded)
            return;

        var existingProviders = GetAvailableProviders().ToList();
        foreach (var providerInfo in providers)
        {
            var wantedIndex = providerInfo.Priority;
            var existingIndex = existingProviders.FindIndex(p => p.Provider == providerInfo.Provider);
            if (existingIndex is -1)
                continue;

            // Enable or disable provider.
            if (providerInfo.Enabled != existingProviders[existingIndex].Enabled)
                existingProviders[existingIndex].Enabled = providerInfo.Enabled;

            // Move index.
            if (wantedIndex != existingIndex)
            {
                var pI = existingProviders[existingIndex];
                existingProviders.RemoveAt(existingIndex);
                if (wantedIndex < 0)
                    existingProviders.Add(pI);
                else
                    existingProviders.Insert(wantedIndex, pI);
            }
        }

        var changed = false;
        var config = configurationProvider.Load();
        var priority = existingProviders.Select(pI => pI.ID).ToList();
        if (config.Priority.Count != priority.Count || config.Priority.Select((p, i) => (p, i)).Any(tuple => priority[tuple.i] != tuple.p))
        {
            config.Priority = priority;
            changed = true;
        }

        var enabled = existingProviders.OrderBy(p => p.ID).ToDictionary(p => p.ID, p => p.Enabled);
        if (config.Enabled.Count != enabled.Count || !config.Enabled.All((tuple) => enabled.TryGetValue(tuple.Key, out var value) && value == tuple.Value))
        {
            config.Enabled = enabled;
            changed = true;
        }

        if (changed)
        {
            lock (_lock)
            {
                _releaseProviderInfos = existingProviders
                    // Create a copy so that we don't affect the original entry
                    .Select(info => new ReleaseProviderInfo()
                    {
                        ID = info.ID,
                        Version = info.Version,
                        Name = info.Name,
                        Description = info.Description,
                        Provider = info.Provider,
                        ConfigurationInfo = info.ConfigurationInfo,
                        PluginInfo = info.PluginInfo,
                        Enabled = info.Enabled,
                        Priority = priority.IndexOf(info.ID),
                    })
                    .ToDictionary(info => info.ID);
                AutoMatchEnabled = _releaseProviderInfos.Values.Any(p => p.Enabled);
            }
            configurationProvider.Save(config);
            if (fireEvent)
                ProvidersUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    #endregion Provider Info

    #region Get Current Data

    public IReadOnlyList<string> GetStoredReleaseProviderNames()
    {
        var hashSet = new HashSet<string>();
        foreach (var releaseInfo in releaseInfoRepository.GetAll())
            foreach (var providerName in releaseInfo.ProviderName.Split('+', StringSplitOptions.None | StringSplitOptions.RemoveEmptyEntries))
                hashSet.Add(providerName);

        return hashSet
            .Order()
            .ToList();
    }

    public IEnumerable<IReleaseInfo> GetAllReleases(IEnumerable<string>? releaseProviders = null)
    {
        var includeProviders = releaseProviders?
            .Where(x => x is { Length: > 1 } && x[0] is not '!')
            .ToList() ?? [];
        var excludeProviders = releaseProviders?
            .Where(x => x is { Length: > 1 } && x[0] is '!')
            .Select(x => x[1..])
            .ToList() ?? [];
        if (includeProviders.Count is 0 && excludeProviders.Count is 0)
            return releaseInfoRepository.GetAll();
        return releaseInfoRepository.GetAll()
            .Where(releaseInfo =>
            {
                if (excludeProviders.Count > 0 && excludeProviders.Any(providerName => releaseInfo.HasProviderName(providerName)))
                    return false;
                if (includeProviders.Count > 0 && includeProviders.Any(providerName => !releaseInfo.HasProviderName(providerName)))
                    return false;
                return true;
            });
    }

    public IReleaseInfo? GetCurrentReleaseForVideo(IVideo video)
        => releaseInfoRepository.GetByEd2kAndFileSize(video.ED2K, video.Size);

    public IReadOnlyList<IReleaseMatchAttempt> GetReleaseMatchAttemptsForVideo(IVideo video)
        => releaseInfoMatchAttemptRepository.GetByEd2kAndFileSize(video.ED2K, video.Size);


    #endregion Get Current Data

    #region Find Release

    public async Task ScheduleFindReleaseForVideo(IVideo video, bool force = false, bool addToMylist = true, bool relocateFiles = true, bool prioritize = false)
    {
        if (!AutoMatchEnabled)
        {
            if (relocateFiles)
                await relocationService.ScheduleAutoRelocationForVideo(video);
            return;
        }

        if (!force && releaseInfoRepository.GetByEd2kAndFileSize(video.ED2K, video.Size) is { } existingRelease)
        {
            if (relocateFiles)
                await relocationService.ScheduleAutoRelocationForVideo(video);
            return;
        }

        // Save the attempt now.
        var startedAt = DateTime.Now;
        var providers = GetAvailableProviders(onlyEnabled: true).ToList();
        var providerList = providers
            .Where(IsProviderCurrentlyUsable)
            .DistinctBy(p => p.ID)
            .Select((provider, index) => new ReleaseProviderInfo()
            {
                ID = provider.ID,
                Version = provider.Version,
                Name = provider.Name,
                Description = provider.Description,
                Provider = provider.Provider,
                ConfigurationInfo = provider.ConfigurationInfo,
                PluginInfo = provider.PluginInfo,
                Enabled = true,
                Priority = index,
            })
            .ToList();
        if (providerList.Count is 0)
        {
            try
            {
                SearchCompleted?.Invoke(this, new()
                {
                    Video = video,
                    ReleaseInfo = null,
                    IsSaved = true,
                    IsAutomatic = true,
                    StartedAt = startedAt,
                    CompletedAt = startedAt,
                    Exception = null,
                    AttemptedProviders = providerList,
                    SelectedProvider = null,
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Got an error in a SearchCompleted event.");
            }
            if (providers.Count is 0)
                logger.LogTrace("No providers enabled during search for video. (Video={VideoID})", video.ID);
            return;
        }

        var matchAttempt = new StoredReleaseInfo_MatchAttempt()
        {
            ED2K = video.ED2K,
            FileSize = video.Size,
            AttemptStartedAt = startedAt,
            AttemptEndedAt = startedAt,
            AttemptedProviderNames = providerList.Select(p => p.Provider.Name).ToList(),
        };
        releaseInfoMatchAttemptRepository.Save(matchAttempt);

        var chain = schedulerFactory.CreateJobChain();
        foreach (var p in providerList)
        {
            if (_providerJobTypes.TryGetValue(p.Provider.GetType(), out var jobType))
                chain.Then(jobType, j => SetProviderJobProps(
                    j,
                    video.ID,
                    matchAttempt.StoredReleaseInfo_MatchAttemptID,
                    addToMylist
                ));
            else
                chain.Then<ProcessReleaseProviderJob>(c =>
                {
                    c.VideoLocalID = video.ID;
                    c.MatchAttemptID = matchAttempt.StoredReleaseInfo_MatchAttemptID;
                    c.SkipMyList = !addToMylist;
                    c.ProviderID = p.ID;
                });
        }

        chain.Then<FinalizeReleaseSearchJob>(c =>
        {
            c.VideoLocalID = video.ID;
            c.MatchAttemptID = matchAttempt.StoredReleaseInfo_MatchAttemptID;
            c.ShouldRelocate = relocateFiles;
        });

        await chain.EnqueueAfterCurrent();
    }

    public void FireSearchCompleted(IVideo video, IReleaseMatchAttempt attempt, IReleaseInfo? releaseInfo = null, Exception? exception = null)
    {
        try
        {
            var allProviders = GetAvailableProviders()
                .DistinctBy(p => p.Name)
                .ToDictionary(p => p.Name);
            var providerList = attempt.AttemptedProviderNames
                .Where(allProviders.ContainsKey)
                .Select(p => allProviders[p])
                .Select((provider, index) => new ReleaseProviderInfo()
                {
                    ID = provider.ID,
                    Version = provider.Version,
                    Name = provider.Name,
                    Description = provider.Description,
                    Provider = provider.Provider,
                    ConfigurationInfo = provider.ConfigurationInfo,
                    PluginInfo = provider.PluginInfo,
                    Enabled = true,
                    Priority = index,
                })
                .ToList();
            var selectedProvider = providerList
                .FirstOrDefault(p => p.ID == attempt.ProviderID);
            var eventArgs = new VideoReleaseSearchCompletedEventArgs()
            {
                Video = video,
                ReleaseInfo = releaseInfo,
                IsSaved = true,
                IsAutomatic = true,
                StartedAt = attempt.AttemptStartedAt,
                CompletedAt = attempt.AttemptEndedAt,
                Exception = exception,
                AttemptedProviders = providerList,
                SelectedProvider = selectedProvider,
            };
            SearchCompleted?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Got an error in a SearchCompleted event.");
        }
    }

    private bool IsProviderCurrentlyUsable(ReleaseProviderInfo provider)
    {
        var jobType = _providerJobTypes.GetValueOrDefault(provider.Provider.GetType()) ?? typeof(ProcessReleaseProviderJob);
        return !schedulerFactory.IsJobTypeBlocked(jobType);
    }

    private static void SetProviderJobProps(IQueueJob job, int videoLocalID, int MatchAttemptID, bool addToMylist)
    {
        var type = job.GetType();
        type.GetProperty(nameof(ProcessReleaseProviderJob.VideoLocalID))?.SetValue(job, videoLocalID);
        type.GetProperty(nameof(ProcessReleaseProviderJob.MatchAttemptID))?.SetValue(job, MatchAttemptID);
        type.GetProperty(nameof(ProcessReleaseProviderJob.SkipMyList))?.SetValue(job, !addToMylist);
    }

    public async Task<bool> TryScheduleRescanForVideo(IVideo video, IReleaseInfo existingRelease, IReleaseMatchAttempt lastAttempt)
    {
        if (existingRelease.PreventRescan) return false;
        foreach (var providerInfo in GetAvailableProviders(onlyEnabled: true))
        {
            var delay = providerInfo.Provider.GetRescanDelay(existingRelease, lastAttempt);
            if (delay is null) continue;
            if (DateTime.Now < lastAttempt.AttemptEndedAt + delay.Value) continue;

            var concreteAttempt = (StoredReleaseInfo_MatchAttempt)lastAttempt;
            concreteAttempt.AttemptCount++;
            releaseInfoMatchAttemptRepository.Save(concreteAttempt);

            var jobType = _providerJobTypes.GetValueOrDefault(providerInfo.Provider.GetType()) ?? typeof(ProcessReleaseProviderJob);
            await schedulerFactory.Enqueue(jobType, j => SetProviderJobProps(j, video.ID, concreteAttempt.StoredReleaseInfo_MatchAttemptID, false));
            return true;
        }
        return false;
    }

    public async Task<IReleaseInfo?> FindReleaseForVideo(IVideo video, bool saveRelease = true, bool addToMylist = true, bool isAutomatic = true, CancellationToken cancellationToken = default)
         => await FindReleaseForVideo(video, GetAvailableProviders(onlyEnabled: true), saveRelease, addToMylist, isAutomatic, cancellationToken);

    public async Task<IReleaseInfo?> FindReleaseForVideo(IVideo video, IEnumerable<ReleaseProviderInfo> providers, bool saveRelease = true, bool addToMylist = true, bool isAutomatic = true, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.Now;
        var completedAt = startedAt;
        var selectedProvider = (ReleaseProviderInfo?)null;
        var releaseInfo = (IReleaseInfo?)null;
        var exception = (Exception?)null;
        var request = new ReleaseInfoContext()
        {
            Video = video,
            IsAutomatic = isAutomatic,
        };
        // Reconfigure the providers in case we got them "out of order" by the
        // user or another plugin. So we can trust the set priority/order later.
        var providerList = providers
            .Where(IsProviderCurrentlyUsable)
            .DistinctBy(p => p.ID)
            .Select((provider, index) => new ReleaseProviderInfo()
            {
                ID = provider.ID,
                Version = provider.Version,
                Name = provider.Name,
                Description = provider.Description,
                Provider = provider.Provider,
                ConfigurationInfo = provider.ConfigurationInfo,
                PluginInfo = provider.PluginInfo,
                Enabled = true,
                Priority = index,
            })
            .ToList();
        var matchAttempt = new StoredReleaseInfo_MatchAttempt()
        {
            ED2K = video.ED2K,
            FileSize = video.Size,
            AttemptStartedAt = startedAt,
            AttemptEndedAt = startedAt,
            AttemptedProviderNames = providerList.Select(p => p.Provider.Name).ToList(),
        };
        try
        {
            if (providerList.Count == 0)
            {
                logger.LogTrace("No providers enabled during search for video. (Video={VideoID})", video.ID);
                return null;
            }

            // Save the attempt now.
            releaseInfoMatchAttemptRepository.Save(matchAttempt);

            (releaseInfo, selectedProvider) = await FileReleaseForVideoSequential(request, providerList, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // If we didn't find a release then save the attempt now.
            if (releaseInfo is null)
                matchAttempt.AttemptEndedAt = DateTime.Now;

            matchAttempt.ProviderName = selectedProvider?.Name;
            matchAttempt.ProviderID = selectedProvider?.ID;
            releaseInfoMatchAttemptRepository.Save(matchAttempt);

            if (!saveRelease || releaseInfo is null)
                return releaseInfo;

            releaseInfo = await SaveReleaseForVideo(video, releaseInfo, matchAttempt, addToMylist);
            completedAt = matchAttempt.AttemptEndedAt;
            return releaseInfo;
        }
        // We're going to re-throw the exception, but we want to make sure we reset the release info and completion time
        // if something went wrong.
        catch (Exception ex)
        {
            releaseInfo = null;
            completedAt = DateTime.Now;
            exception = ex;

            matchAttempt.AttemptEndedAt = DateTime.Now;
            releaseInfoMatchAttemptRepository.Save(matchAttempt);
            throw;
        }
        finally
        {
            // We don't want the search started/completed events to interrupt the search, so wrap them both in a try…catch block.
            try
            {
                SearchCompleted?.Invoke(this, new()
                {
                    Video = video,
                    ReleaseInfo = releaseInfo,
                    IsSaved = saveRelease,
                    IsAutomatic = isAutomatic,
                    StartedAt = startedAt,
                    CompletedAt = completedAt,
                    Exception = exception,
                    AttemptedProviders = providerList,
                    SelectedProvider = selectedProvider,
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Got an error in a SearchCompleted event.");
            }
        }
    }

    private async Task<(IReleaseInfo?, ReleaseProviderInfo?)> FileReleaseForVideoSequential(ReleaseInfoContext request, IReadOnlyList<ReleaseProviderInfo> providers, CancellationToken cancellationToken)
    {
        foreach (var providerInfo in providers)
        {
            logger.LogTrace("Trying to find release for video using provider {ProviderName}. (Video={VideoID}.Provider={ProviderID})", providerInfo.Provider.Name, request.Video.ID, providerInfo.ID);
            var provider = providerInfo.Provider;
            var release = await provider.GetReleaseInfoForVideo(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (release is null || release.CrossReferences.Count < 1)
                continue;

            logger.LogTrace("Selected release for video using provider {ProviderName}. (Video={VideoID}.Provider={ProviderID})", providerInfo.Provider.Name, request.Video.ID, providerInfo.ID);
            return (new ReleaseInfoWithProvider(release, provider.Name), providerInfo);
        }

        return default;
    }

    #endregion Find Release

    #region Save Release

    public Task<IReleaseInfo> SaveReleaseForVideo(IVideo video, ReleaseInfo release, string providerName = "User", bool addToMylist = true)
        => SaveReleaseForVideo(video, new ReleaseInfoWithProvider(release, providerName), addToMylist);

    public async Task<IReleaseInfo> SaveReleaseForVideo(IVideo video, IReleaseInfo release, bool addToMylist = true)
        => await SaveReleaseForVideo(video, release, new() { ProviderName = release.ProviderName, EmbeddedAttemptProviderNames = release.ProviderName, AttemptStartedAt = DateTime.UtcNow, AttemptEndedAt = DateTime.UtcNow, ED2K = video.ED2K, FileSize = video.Size }, addToMylist);

    internal async Task<IReleaseInfo> SaveReleaseForVideo(IVideo video, IReleaseInfo release, StoredReleaseInfo_MatchAttempt matchAttempt, bool addToMylist = true)
    {
        if (release.CrossReferences.Count < 1)
            throw new InvalidOperationException("Release must have at least one valid cross reference.");

        var releaseInfo = new StoredReleaseInfo(video, release);
        if (!CheckCrossReferences(video, releaseInfo, out var legacyXrefs))
            throw new InvalidOperationException($"Release have {release.CrossReferences.Count - legacyXrefs.Count} invalid cross reference(s).");

        var missingGroupId = CheckReleaseGroup(releaseInfo);

        // Ensure we don't have an empty list of hashes.
        if (releaseInfo.Hashes is { } hashes)
        {
            hashes = hashes.Where(h => !string.IsNullOrEmpty(h.Value) && !string.IsNullOrEmpty(h.Type)).ToList();
            releaseInfo.Hashes = hashes.Count < 1 ? null : hashes;
        }

        var lastImportedAt = (DateTime?)null;
        var releaseUriMatches = false;
        if (releaseInfoRepository.GetByEd2kAndFileSize(video.ED2K, video.Size) is { } existingRelease)
        {
            // If the new release info is **EXACTLY** the same as the existing one, then just return the existing one.
            if (existingRelease == releaseInfo)
            {
                matchAttempt.AttemptEndedAt = DateTime.Now;
                releaseInfoRepository.Save(existingRelease);
                releaseInfoMatchAttemptRepository.Save(matchAttempt);
                return existingRelease;
            }

            releaseUriMatches = string.Equals(existingRelease.ReleaseURI, releaseInfo.ReleaseURI);

            // Re-use the last imported at date if the release URI matches between the old and new release.
            if (releaseUriMatches && video is VideoLocal vl0 && vl0.DateTimeImported is not null)
                lastImportedAt = vl0.DateTimeImported;

            releaseInfo.PreventRescan = releaseInfo.PreventRescan || existingRelease.PreventRescan;
            await ClearReleaseForVideo(video, existingRelease, removeFromMylist: addToMylist && !releaseUriMatches);
        }

        // Make sure the revision is valid.
        if (releaseInfo.Version < 1)
            releaseInfo.Version = 1;

        releaseInfo.LastUpdatedAt = DateTime.Now;
        matchAttempt.AttemptEndedAt = release.LastUpdatedAt;
        releaseInfoRepository.Save(releaseInfo);
        releaseInfoMatchAttemptRepository.Save(matchAttempt);
        xrefRepository.Save(legacyXrefs);

        // Mark the video as imported if needed.
        if (video is VideoLocal videoLocal && videoLocal.DateTimeImported is null)
        {
            videoLocal.DateTimeImported = lastImportedAt ?? DateTime.Now;
            videoRepository.Save(videoLocal);
        }

        // Schedule the release group to be fetched if needed.
        if (missingGroupId is not null)
            await schedulerFactory.RunAfterCurrent<GetAniDBReleaseGroupJob>(c => c.GroupID = missingGroupId.Value);

        await ScheduleAnimeForRelease(legacyXrefs);

        SetWatchedStateIfNeeded(video, releaseInfo);

        // Sync to mylist if needed.
        if (addToMylist && !releaseUriMatches && _settings.AniDb.MyList_AddFiles)
            await schedulerFactory.StartJob<AddFileToMyListJob>(c =>
            {
                c.Hash = video.ED2K;
                c.ReadStates = true;
            }).ConfigureAwait(false);
        // Rename and/or move the physical file(s) if needed.
        await relocationService.ChainAutoRelocationForVideo(video).ConfigureAwait(false);

        try
        {
            // Dispatch the release saved event now.
            ReleaseSaved?.Invoke(null, new() { Video = video, ReleaseInfo = releaseInfo });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Got an error in a ReleaseSaved event.");
        }

        return releaseInfo;
    }

    #region Save Release | Internals

    private bool CheckCrossReferences(IVideo video, StoredReleaseInfo releaseInfo, out List<CrossRef_File_Episode> legacyXrefs)
    {
        legacyXrefs = [];

        var edgeCases = 0;
        var legacyOrder = 0;
        var fileName = (video.Files.FirstOrDefault(loc => loc.IsAvailable) ?? video.Files.FirstOrDefault())?.FileName ?? string.Empty;
        var checkedIDs = new HashSet<int>();
        var embeddedXrefs = new List<EmbeddedCrossReference>();
        foreach (var xrefGroup in releaseInfo.CrossReferences.OfType<EmbeddedCrossReference>().GroupBy(xref => xref.AnidbEpisodeID))
        {
            var firstXref = xrefGroup.First();
            if (firstXref.AnidbEpisodeID is <= 0)
            {
                logger.LogError("Negative or zero episode id: {EpisodeID}!", firstXref.AnidbEpisodeID);
                continue;
            }

            if (_unknownEpisodeIDs.Contains(firstXref.AnidbEpisodeID))
            {
                logger.LogError("Unknown episode id: {EpisodeID}!", firstXref.AnidbEpisodeID);
                continue;
            }

            // If the provider doesn't know which anime the episode belongs to, then try to look it up.
            var animeID = xrefGroup
                .Select(xref => xref.AnidbAnimeID ?? 0)
                .Where(animeID => animeID > 0)
                .GroupBy(animeID => animeID)
                .OrderByDescending(groupBy => groupBy.Count())
                .FirstOrDefault()?
                .Key;
            if (animeID is null)
            {
                if (anidbEpisodeRepository.GetByEpisodeID(firstXref.AnidbEpisodeID) is { } episode)
                {
                    animeID = episode.AnimeID;
                }
                else if (udpConnection.IsBanned)
                {
                    logger.LogInformation("Could not get AnimeID for episode {EpisodeID}, but we're UDP banned, so deferring fetch to later!", firstXref.AnidbEpisodeID);
                }
                else
                {
                    logger.LogInformation("Could not get AnimeID for episode {EpisodeID}, downloading more info…", firstXref.AnidbEpisodeID);
                    try
                    {
                        var episodeResponse = requestFactory
                            .Create<RequestGetEpisode>(r => r.EpisodeID = firstXref.AnidbEpisodeID)
                            .Send();
                        animeID = episodeResponse.Response?.AnimeID;
                        if (episodeResponse.Code is UDPReturnCode.NO_SUCH_EPISODE)
                        {
                            logger.LogError("Unknown episode with id {EpisodeID}!", firstXref.AnidbEpisodeID);
                            _unknownEpisodeIDs.Add(firstXref.AnidbEpisodeID);
                            continue;
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Could not get Episode Info for {EpisodeID}!", firstXref.AnidbEpisodeID);
                    }
                }
            }

            var xrefList = new List<EmbeddedCrossReference>();
            foreach (var xref in xrefGroup)
            {
                // The percentage range cannot be 0.
                if (xref.PercentageEnd == xref.PercentageStart)
                    continue;

                // Reverse the percentage range if it is backwards.
                if (xref.PercentageEnd < xref.PercentageStart)
                    (xref.PercentageEnd, xref.PercentageStart) = (xref.PercentageStart, xref.PercentageEnd);

                // The percentage range must be between 0 and 100.
                if (xref.PercentageEnd > 100)
                    xref.PercentageEnd = 100;
                if (xref.PercentageStart < 0)
                    xref.PercentageStart = 0;

                xrefList.Add(xref);
            }

            // This clause is for the cases where a file is linked 100% to an episode, and have an
            // additional relation that's not 100% to the same episode.
            if (
                xrefList.Count > 1 &&
                xrefList.Any(xref => xref is { PercentageStart: 0, PercentageEnd: 100 }) &&
                xrefList.Any(xref => xref is not { PercentageStart: 0, PercentageEnd: 100 })
            )
            {
                while (xrefList.FindIndex(xref => xref is { PercentageStart: 0, PercentageEnd: 100 }) is { } index && index is not -1)
                {
                    xrefList.RemoveAt(index);
                    edgeCases++;
                }
            }

            foreach (var xref in xrefList.DistinctBy(xref => (xref.PercentageStart, xref.PercentageEnd)))
            {
                // If we got this far and the anime ID is set, then apply it now.
                if (animeID is not null)
                    xref.AnidbAnimeID = animeID;

                embeddedXrefs.Add(xref);
                legacyXrefs.Add(new()
                {
                    Hash = video.ED2K,
                    AnimeID = animeID ?? 0,
                    EpisodeID = xref.AnidbEpisodeID,
                    Percentage = xref.PercentageEnd - xref.PercentageStart,
                    EpisodeOrder = legacyOrder++,
                    FileName = fileName,
                    FileSize = video.Size,
                });
            }
        }

        if (embeddedXrefs.Count != releaseInfo.CrossReferences.Count - edgeCases)
            return false;

        releaseInfo.CrossReferences = embeddedXrefs;
        return true;
    }

    private int? CheckReleaseGroup(StoredReleaseInfo releaseInfo)
    {
        if (string.IsNullOrEmpty(releaseInfo.GroupID) || string.IsNullOrEmpty(releaseInfo.GroupSource))
        {
            releaseInfo.GroupID = null;
            releaseInfo.GroupSource = null;
            releaseInfo.GroupName = null;
            releaseInfo.GroupShortName = null;
            return null;
        }

        if (
            !GetAniDBReleaseGroupJob.InvalidReleaseGroupNames.Contains(releaseInfo.GroupName) &&
            !GetAniDBReleaseGroupJob.InvalidReleaseGroupNames.Contains(releaseInfo.GroupShortName) &&
            !string.IsNullOrEmpty(releaseInfo.GroupName) &&
            !string.IsNullOrEmpty(releaseInfo.GroupShortName)
        )
            return null;

        // If we have an existing release from the group with valid names, use that.
        var existingReleasesForGroup = releaseInfoRepository.GetByGroupAndProviderIDs(releaseInfo.GroupID, releaseInfo.GroupSource)
            .Where(rI => !string.IsNullOrEmpty(rI.GroupName) && !string.IsNullOrEmpty(rI.GroupShortName))
            .OrderByDescending(rI => rI.LastUpdatedAt)
            .ToList();
        if (existingReleasesForGroup.Count > 0)
        {
            releaseInfo.GroupName = existingReleasesForGroup[0].GroupName;
            releaseInfo.GroupShortName = existingReleasesForGroup[0].GroupShortName;
            return null;
        }

        // Remove the group info if it's not from AniDB and doesn't have a valid name/short name.
        if (releaseInfo.GroupSource is not "AniDB" || !int.TryParse(releaseInfo.GroupID, out var groupID) || groupID <= 0)
        {
            releaseInfo.GroupID = null;
            releaseInfo.GroupSource = null;
            releaseInfo.GroupName = null;
            releaseInfo.GroupShortName = null;
            return null;
        }

        // Otherwise try to fetch group info from AniDB.
        try
        {
            var response = requestFactory
                .Create<RequestReleaseGroup>(r => r.ReleaseGroupID = groupID)
                .Send();
            if (response.Response is not null)
            {
                if (
                    !string.IsNullOrEmpty(response.Response.Name) &&
                    !string.IsNullOrEmpty(response.Response.ShortName) &&
                    !GetAniDBReleaseGroupJob.InvalidReleaseGroupNames.Contains(response.Response.Name) &&
                    !GetAniDBReleaseGroupJob.InvalidReleaseGroupNames.Contains(response.Response.ShortName)
                )
                {
                    releaseInfo.GroupName = response.Response.Name;
                    releaseInfo.GroupShortName = response.Response.ShortName;
                    return null;
                }
                else
                {
                    releaseInfo.GroupID = null;
                    releaseInfo.GroupSource = null;
                    releaseInfo.GroupName = null;
                    releaseInfo.GroupShortName = null;
                }
            }
            else
            {
                releaseInfo.GroupID = null;
                releaseInfo.GroupSource = null;
                releaseInfo.GroupName = null;
                releaseInfo.GroupShortName = null;
            }
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not get IReleaseGroup info from AniDB for {GroupID}!", groupID);

            releaseInfo.GroupName = null;
            releaseInfo.GroupShortName = null;

            return groupID;
        }
    }

    private async Task ScheduleAnimeForRelease(IReadOnlyList<IVideoCrossReference> xrefs)
    {
        var animeIDs = xrefs
            .GroupBy(xref => xref.AnidbAnimeID)
            .ExceptBy([0], groupBy => groupBy.Key)
            .ToDictionary(
                groupBy => groupBy.Key,
                groupBy =>
                    anidbAnimeRepository.GetByAnimeID(groupBy.Key) is null ||
                    shokoSeriesRepository.GetByAnimeID(groupBy.Key) is null ||
                    anidbAnimeUpdateRepository.GetByAnimeID(groupBy.Key) is null ||
                    groupBy.Any(xref => xref.AnidbEpisode is null || xref.ShokoEpisode is null)
            );
        if (animeIDs.Count == 0)
            return;
        var refreshMethod = AnidbRefreshMethod.Default | AnidbRefreshMethod.CreateShokoSeries;
        if (_settings.AutoGroupSeries || _settings.AniDb.DownloadRelatedAnime)
            refreshMethod |= AnidbRefreshMethod.DownloadRelations;
        foreach (var (animeID, missingEpisodes) in animeIDs)
        {
            var animeRecentlyUpdated = false;
            var update = anidbAnimeUpdateRepository.GetByAnimeID(animeID)!;
            if (!missingEpisodes && (DateTime.Now - update.UpdatedAt).TotalHours < _settings.AniDb.MinimumHoursToRedownloadAnimeInfo)
                animeRecentlyUpdated = true;

            // even if we are missing episode info, don't get data  more than once every `x` hours
            // this is to prevent banning
            if (missingEpisodes)
            {
                logger.LogInformation("Queuing immediate GET for AniDB_Anime: {AnimeID}", animeID);
                _anidbService ??= serviceProvider.GetRequiredService<IAnidbService>();
                await _anidbService.ScheduleRefreshOfAnimeByID(animeID, refreshMethod, prioritize: true);
            }
            else if (!animeRecentlyUpdated)
            {
                logger.LogInformation("Queuing GET for AniDB_Anime: {AnimeID}", animeID);
                _anidbService ??= serviceProvider.GetRequiredService<IAnidbService>();
                await _anidbService.ScheduleRefreshOfAnimeByID(animeID, refreshMethod);
            }
            else
            {
                await schedulerFactory.RunAfterCurrent<RefreshAnimeStatsJob>(b => b.AnimeID = animeID);
            }

            var tmdbShowXrefs = crossRefAnidbTmdbRepository.GetByAnidbAnimeID(animeID);
            foreach (var xref in tmdbShowXrefs)
                await schedulerFactory.RunAfterCurrent<UpdateTmdbShowJob>(job =>
                {
                    job.TmdbShowID = xref.TmdbShowID;
                    job.DownloadImages = true;
                }).ConfigureAwait(false);
        }
    }

    private void SetWatchedStateIfNeeded(IVideo video, IReleaseInfo releaseInfo)
    {
        if (!_settings.Import.UseExistingFileWatchedStatus) return;

        var otherVideos = releaseInfo.CrossReferences
            .SelectMany(xref => videoRepository.GetByAniDBEpisodeID(xref.AnidbEpisodeID))
            .WhereNotNull()
            .ExceptBy([video.ED2K], v => v.Hash)
            .Cast<IVideo>()
            .ToList();

        if (otherVideos.Count == 0)
            return;

        _userDataService ??= serviceProvider.GetRequiredService<IUserDataService>();
        foreach (var user in userService.GetUsers())
        {
            var watchedVideo = otherVideos
                .FirstOrDefault(video => _userDataService.GetVideoUserData(video, user)?.LastPlayedAt is not null);
            if (watchedVideo is null)
                continue;

            var watchedRecord = _userDataService.GetVideoUserData(watchedVideo, user)!;
            _userDataService.ImportVideoUserData(video, user, new(watchedRecord), "Video", false);
        }
    }

    #endregion Save Release | Internals

    #endregion Save Release

    #region Clear Release

    public async Task ClearReleaseForVideo(IVideo video, bool removeFromMylist = true)
    {
        if (releaseInfoRepository.GetByEd2kAndFileSize(video.ED2K, video.Size) is { } existingRelease)
            await ClearReleaseForVideo(video, existingRelease, removeFromMylist);
    }

    public async Task PurgeUsedReleases(IEnumerable<string>? providerNames = null, bool removeFromMylist = true)
    {
        var providerNameSet = providerNames?.ToHashSet();
        var releases = releaseInfoRepository.GetAll()
            .Select(release => videoRepository.GetByEd2kAndSize(release.ED2K, release.FileSize) is { } video ? (video, release) : (video: null, release))
            .Where(v => v.video is not null && (providerNameSet is null || providerNameSet.Contains(v.release.ProviderName)))
            .ToList();
        foreach (var (video, release) in releases)
            await ClearReleaseForVideo(video, release, removeFromMylist);
    }

    public async Task PurgeUnusedReleases(IEnumerable<string>? providerNames = null, bool removeFromMylist = true)
    {
        var providerNameSet = providerNames?.ToHashSet();
        var releases = releaseInfoRepository.GetAll()
            .Where(v => videoRepository.GetByEd2kAndSize(v.ED2K, v.FileSize) is null)
            .Where(release => providerNameSet is null || providerNameSet.Contains(release.ProviderName))
            .ToList();
        foreach (var release in releases)
            await ClearReleaseForVideo(null, release, removeFromMylist);
    }

    public async Task RemoveRelease(IReleaseInfo releaseInfo, bool removeFromMylist = true)
    {
        if (releaseInfo is not StoredReleaseInfo storedReleaseInfo)
            return;

        var video = videoRepository.GetByEd2kAndSize(storedReleaseInfo.ED2K, storedReleaseInfo.FileSize);
        await ClearReleaseForVideo(video, storedReleaseInfo, removeFromMylist);
    }

    private async Task ClearReleaseForVideo(IVideo? video, StoredReleaseInfo releaseInfo, bool removeFromMylist = true)
    {
        // Mark the video as not imported if the video hasn't been deleted from the database,
        // because the clear method can still be called after the video has been deleted.
        if (video is VideoLocal videoLocal && videoRepository.GetByID(video.ID) is not null)
        {
            videoLocal.DateTimeImported = null;
            videoRepository.Save(videoLocal);
        }

        var xrefs = xrefRepository.GetByEd2k(releaseInfo.ED2K);
        xrefRepository.Delete(xrefs);

        releaseInfoRepository.Delete(releaseInfo);

        if (removeFromMylist)
            await RemoveFromMyList(releaseInfo);

        await ScheduleAnimeForRelease(xrefs);

        try
        {
            ReleaseDeleted?.Invoke(null, new() { Video = video, ReleaseInfo = releaseInfo });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Got an error in a ReleaseDeleted event.");
        }
    }

    #region Clear Release | Internals

    private async Task RemoveFromMyList(StoredReleaseInfo releaseInfo)
    {
        if (_settings.AniDb.MyList_DeleteType is AniDBFileDeleteType.DeleteLocalOnly)
        {
            logger.LogInformation("Keeping physical file and AniDB MyList entry, deleting from local DB: Hash: {Hash}", releaseInfo.ED2K);
            return;
        }
        if (releaseInfo is { ReleaseURI: not null } && releaseInfo.ReleaseURI.StartsWith(AnidbReleaseProvider.ReleasePrefix))
        {
            await schedulerFactory.StartJob<DeleteFileFromMyListJob>(c =>
                {
                    c.Hash = releaseInfo.ED2K;
                    c.FileSize = releaseInfo.FileSize;
                }
            );
        }
        else
        {
            foreach (var xref in releaseInfo.CrossReferences)
            {
                if (xref.AnidbAnimeID is null or 0)
                    continue;

                var anidbEpisode = anidbEpisodeRepository.GetByEpisodeID(xref.AnidbEpisodeID);
                if (anidbEpisode is null)
                    continue;

                await schedulerFactory.StartJob<DeleteFileFromMyListJob>(c =>
                    {
                        c.AnimeID = xref.AnidbAnimeID.Value;
                        c.EpisodeType = anidbEpisode.EpisodeType;
                        c.EpisodeNumber = anidbEpisode.EpisodeNumber;
                    }
                );
            }
        }
    }

    #endregion Clear Release | Internals

    #endregion Clear Release

    #region ID Helpers

    private Guid GetID(Type providerType)
        => _loaded && pluginManager.GetPluginInfo(providerType.Assembly) is { } pluginInfo
            ? GetID(providerType, pluginInfo)
            : Guid.Empty;

    private static Guid GetID(Type type, LocalPluginInfo pluginInfo)
        => UuidUtility.GetV5($"ReleaseProvider={type.FullName!}", pluginInfo.ID);

    #endregion ID Helpers
}
