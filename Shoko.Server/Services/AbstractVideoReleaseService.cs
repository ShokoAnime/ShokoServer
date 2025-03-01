
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Models.Release;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Scheduling.Jobs.TMDB;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Services;

public class AbstractVideoReleaseService(
    ILogger<AbstractVideoReleaseService> logger,
    IUDPConnectionHandler udpConnection,
    ISettingsProvider settingsProvider,
    ISchedulerFactory schedulerFactory,
    IRequestFactory requestFactory,
    IUserService userService,
    IUserDataService userDataService,
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
    private IServerSettings _settings => settingsProvider.GetSettings();

    private Dictionary<Guid, IReleaseInfoProvider>? _releaseInfoProviders = null;

    public event EventHandler<VideoReleaseEventArgs>? ReleaseSaved;

    public event EventHandler<VideoReleaseEventArgs>? ReleaseDeleted;

    public event EventHandler<VideoReleaseSearchStartedEventArgs>? SearchStarted;

    public event EventHandler<VideoReleaseSearchCompletedEventArgs>? SearchCompleted;

    public event EventHandler? ProvidersUpdated;

    private bool? _autoMatchEnabled = null;

    public bool AutoMatchEnabled
        => _autoMatchEnabled ??= GetAvailableProviders(true).Any();

    public bool ParallelMode
    {
        get => _settings.Plugins.ReleaseProviders.ParallelMode;
        set
        {
            if (_settings.Plugins.ReleaseProviders.ParallelMode == value)
                return;

            _autoMatchEnabled = null;
            _settings.Plugins.ReleaseProviders.ParallelMode = value;
            settingsProvider.SaveSettings(_settings);
            ProvidersUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    public void AddProviders(IEnumerable<IReleaseInfoProvider> providers)
    {
        if (_releaseInfoProviders is not null)
            return;

        _releaseInfoProviders = providers.ToDictionary(GetID);

        ProvidersUpdated?.Invoke(this, EventArgs.Empty);
    }

    public IEnumerable<ReleaseInfoProviderInfo> GetAvailableProviders(bool onlyEnabled = false)
    {
        if (_releaseInfoProviders is null)
            yield break;

        var order = _settings.Plugins.ReleaseProviders.Priority;
        var enabled = _settings.Plugins.ReleaseProviders.Enabled;
        var orderedProviders = _releaseInfoProviders
            .OrderBy(p => order.IndexOf(p.Key) is -1)
            .ThenBy(p => order.IndexOf(p.Key))
            .ThenBy(p => p.Key)
            .Select((provider, index) => (provider, index));
        foreach (var ((id, provider), priority) in orderedProviders)
        {
            var isEnabled = enabled.TryGetValue(id, out var enabledValue) ? enabledValue : provider.Name is "AniDB";
            if (onlyEnabled && !isEnabled)
                continue;

            yield return new()
            {
                ID = id,
                Provider = provider,
                Enabled = isEnabled,
                Priority = priority,
            };
        }
    }

    public ReleaseInfoProviderInfo GetProviderInfo(IReleaseInfoProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (_releaseInfoProviders is null)
            throw new InvalidOperationException("Providers have not been added yet.");

        return GetProviderInfo(GetID(provider))
            ?? throw new ArgumentException($"Unregistered provider: '{provider.GetType().Name}'", nameof(provider));
    }

    public ReleaseInfoProviderInfo GetProviderInfo<TProvider>() where TProvider : class, IReleaseInfoProvider
    {
        if (_releaseInfoProviders is null)
            throw new InvalidOperationException("Providers have not been added yet.");

        return GetProviderInfo(GetID(typeof(TProvider)))
            ?? throw new ArgumentException($"Unregistered provider: '{typeof(TProvider).Name}'", nameof(TProvider));
    }

    public ReleaseInfoProviderInfo? GetProviderInfo(Guid providerID)
    {
        if (_releaseInfoProviders is null || !_releaseInfoProviders.TryGetValue(providerID, out var provider))
            return null;

        // We update the settings upon server start to ensure the priority and enabled states are accurate, so trust them.
        var priority = _settings.Plugins.ReleaseProviders.Priority.IndexOf(providerID);
        var enabled = _settings.Plugins.ReleaseProviders.Enabled.TryGetValue(providerID, out var isEnabled) ? isEnabled : provider.Name is "AniDB";
        return new()
        {
            ID = providerID,
            Provider = provider,
            Enabled = enabled,
            Priority = priority,
        };
    }

    public void UpdateProviders(params ReleaseInfoProviderInfo[] providers)
    {
        if (_releaseInfoProviders is null)
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
        var settings = settingsProvider.GetSettings();
        var priority = existingProviders.Select(pI => pI.ID).ToList();
        if (!settings.Plugins.ReleaseProviders.Priority.SequenceEqual(priority))
        {
            settings.Plugins.ReleaseProviders.Priority = priority;
            changed = true;
        }

        var enabled = existingProviders.ToDictionary(p => p.ID, p => p.Enabled);
        if (!settings.Plugins.ReleaseProviders.Enabled.SequenceEqual(enabled))
        {
            settings.Plugins.ReleaseProviders.Enabled = enabled;
            changed = true;
        }

        if (changed)
        {
            _autoMatchEnabled = null;
            settingsProvider.SaveSettings(settings);
            ProvidersUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    public IReleaseInfo? GetCurrentReleaseForVideo(IVideo video)
        => releaseInfoRepository.GetByEd2kAndFileSize(video.Hashes.ED2K, video.Size);

    public async Task<IReleaseInfo?> FindReleaseForVideo(IVideo video, bool saveRelease = true, CancellationToken cancellationToken = default)
    {
        // We don't want the search started/completed events to interrupt the search, so wrap them both in a try…catch block.
        var startedAt = DateTime.Now;
        try
        {
            SearchStarted?.Invoke(this, new()
            {
                ShouldSave = saveRelease,
                StartedAt = startedAt,
                Video = video,
            });
        }
        catch { }

        var completedAt = startedAt;
        var selectedProvider = (ReleaseInfoProviderInfo?)null;
        var releaseInfo = (IReleaseInfo?)null;
        var exception = (Exception?)null;
        var providers = GetAvailableProviders(onlyEnabled: true).ToList();
        try
        {
            if (providers.Count == 0)
            {
                logger.LogTrace("No providers enabled during search for video. (Video={VideoID})", video.ID);
                return null;
            }

            (releaseInfo, selectedProvider) = ParallelMode
                ? await FileReleaseForVideoParallel(video, providers, cancellationToken)
                : await FileReleaseForVideoSequential(video, providers, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            var matchAttempt = new StoredReleaseInfo_MatchAttempt()
            {
                ProviderName = selectedProvider?.Provider.Name,
                ED2K = video.Hashes.ED2K,
                FileSize = video.Size,
                AttemptStartedAt = startedAt,
                // Reuse startedAt because it will be overwritten in SaveReleaseForVideo later.
                AttemptEndedAt = releaseInfo is null ? DateTime.Now : startedAt,
                AttemptedProviderNames = providers.Select(p => p.Provider.Name).ToList(),
            };
            // If we didn't find a release then save the attempt now.
            if (releaseInfo is null)
                releaseInfoMatchAttemptRepository.Save(matchAttempt);

            if (!saveRelease || releaseInfo is null)
                return releaseInfo;

            releaseInfo = await SaveReleaseForVideo(video, releaseInfo, matchAttempt);
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
            throw;
        }
        finally
        {
            try
            {
                SearchCompleted?.Invoke(this, new()
                {
                    Video = video,
                    ReleaseInfo = releaseInfo,
                    IsSaved = saveRelease,
                    StartedAt = startedAt,
                    CompletedAt = completedAt,
                    Exception = exception,
                    AttemptedProviders = providers,
                    SelectedProvider = selectedProvider,
                });
            }
            catch { }
        }
    }

    private async Task<(IReleaseInfo?, ReleaseInfoProviderInfo?)> FileReleaseForVideoSequential(IVideo video, IReadOnlyList<ReleaseInfoProviderInfo> providers, CancellationToken cancellationToken)
    {
        foreach (var providerInfo in providers)
        {
            logger.LogTrace("Trying to find release for video using provider {ProviderName}. (Video={VideoID}.Provider={ProviderID})", providerInfo.Provider.Name, video.ID, providerInfo.ID);
            var provider = providerInfo.Provider;
            var release = await provider.GetReleaseInfoForVideo(video, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (release is null || release.CrossReferences.Count < 1)
                continue;

            logger.LogTrace("Selected release for video using provider {ProviderName}. (Video={VideoID}.Provider={ProviderID})", providerInfo.Provider.Name, video.ID, providerInfo.ID);
            return (new ReleaseInfoWithProvider(release, provider.Name), providerInfo);
        }

        return default;
    }

    private async Task<(IReleaseInfo?, ReleaseInfoProviderInfo?)> FileReleaseForVideoParallel(IVideo video, IReadOnlyList<ReleaseInfoProviderInfo> providers, CancellationToken cancellationToken)
    {
        // Start as many providers as possible in parallel until we've exhausted the list or the token is cancelled.
        var tasks = new Dictionary<Task<IReleaseInfo?>, (ReleaseInfoProviderInfo providerInfo, CancellationTokenSource source)>();
        foreach (var providerInfo in providers)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var source = new CancellationTokenSource();
            cancellationToken.Register(source.Cancel);
            var task = Task.Run<IReleaseInfo?>(async () =>
            {
                logger.LogTrace("Trying to find release for video using provider {ProviderName}. (Video={VideoID}.Provider={ProviderID})", providerInfo.Provider.Name, video.ID, providerInfo.ID);
                var release = await providerInfo.Provider.GetReleaseInfoForVideo(video, source.Token);
                if (release is not null && release.CrossReferences.Count > 0)
                {
                    logger.LogTrace("Found release for video using provider {ProviderName}. (Video={VideoID}.Provider={ProviderID})", providerInfo.Provider.Name, video.ID, providerInfo.ID);
                    return new ReleaseInfoWithProvider(release, providerInfo.Provider.Name);
                }

                return null;
            }, source.Token);
            tasks.Add(task, (providerInfo, source));
        }

        // Wait for the highest priority release to be found or for all the tasks to be cancelled.
        var selectedRelease = (IReleaseInfo?)null;
        var selectedProvider = (ReleaseInfoProviderInfo?)null;
        var queue = tasks.Keys.ToList();
        while (queue.Count > 0)
        {
            var task = await Task.WhenAny(queue);
            queue.Remove(task);
            var (providerInfo, source) = tasks[task];
            if (source.IsCancellationRequested)
                continue;

            var releaseInfo = task.Result;
            if (releaseInfo is not null && (selectedProvider is null || providerInfo.Priority < selectedProvider.Priority))
            {
                selectedRelease = releaseInfo;
                selectedProvider = providerInfo;
            }

            foreach (var item in tasks.Values.Where(tuple => tuple.providerInfo.Priority >= providerInfo.Priority))
                item.source.Cancel();
        }

        if (selectedRelease is not null && selectedProvider is not null)
            logger.LogTrace("Selected release for video using provider {ProviderName}. (Video={VideoID}.Provider={ProviderID})", selectedProvider.Provider.Name, video.ID, selectedProvider.ID);

        return (selectedRelease, selectedProvider);
    }

    public Task<IReleaseInfo> SaveReleaseForVideo(IVideo video, ReleaseInfo release, string providerName = "User")
        => SaveReleaseForVideo(video, new ReleaseInfoWithProvider(release, providerName));

    public async Task<IReleaseInfo> SaveReleaseForVideo(IVideo video, IReleaseInfo release)
        => await SaveReleaseForVideo(video, release, new() { ProviderName = release.ProviderName, EmbeddedAttemptProviderNames = release.ProviderName, AttemptStartedAt = DateTime.UtcNow, AttemptEndedAt = DateTime.UtcNow, ED2K = video.Hashes.ED2K, FileSize = video.Size });

    private async Task<IReleaseInfo> SaveReleaseForVideo(IVideo video, IReleaseInfo release, StoredReleaseInfo_MatchAttempt matchAttempt)
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

        if (releaseInfoRepository.GetByEd2kAndFileSize(video.Hashes.ED2K, video.Size) is { } existingRelease)
        {
            // If the new release info is **EXACTLY** the same as the existing one, then just return the existing one.
            if (existingRelease == releaseInfo)
            {
                existingRelease.LastUpdatedAt = DateTime.Now;
                matchAttempt.AttemptEndedAt = existingRelease.LastUpdatedAt;
                releaseInfoRepository.Save(existingRelease);
                releaseInfoMatchAttemptRepository.Save(matchAttempt);
                return existingRelease;
            }

            await ClearReleaseForVideo(video, existingRelease);
        }

        // Make sure the revision is valid.
        if (releaseInfo.Revision < 1)
            releaseInfo.Revision = 1;

        releaseInfo.LastUpdatedAt = DateTime.Now;
        matchAttempt.AttemptEndedAt = release.LastUpdatedAt;
        releaseInfoRepository.Save(releaseInfo);
        releaseInfoMatchAttemptRepository.Save(matchAttempt);
        xrefRepository.Save(legacyXrefs);

        // Mark the video as imported if needed.
        var scheduler = await schedulerFactory.GetScheduler();
        if (video is VideoLocal videoLocal && videoLocal.DateTimeImported is null)
        {
            videoLocal.DateTimeImported = DateTime.Now;
            videoRepository.Save(videoLocal);
        }

        // Schedule the release group to be fetched if needed.
        if (missingGroupId is not null)
            await scheduler.StartJob<GetAniDBReleaseGroupJob>(c => c.GroupID = missingGroupId.Value);

        await ScheduleAnimeForRelease(legacyXrefs);

        SetWatchedStateIfNeeded(video, releaseInfo);

        // Sync to mylist if needed.
        if (_settings.AniDb.MyList_AddFiles)
            await scheduler.StartJob<AddFileToMyListJob>(c =>
            {
                c.Hash = video.Hashes.ED2K;
                c.ReadStates = true;
            }).ConfigureAwait(false);
        // Rename and/or move the physical file(s) if needed.
        if (_settings.Plugins.Renamer.RelocateOnImport)
            await scheduler.StartJob<RenameMoveFileJob>(job => job.VideoLocalID = video.ID).ConfigureAwait(false);

        // Dispatch the release saved event now.
        ReleaseSaved?.Invoke(null, new() { Video = video, ReleaseInfo = releaseInfo });

        return releaseInfo;
    }

    public async Task ClearReleaseForVideo(IVideo video)
    {
        if (releaseInfoRepository.GetByEd2kAndFileSize(video.Hashes.ED2K, video.Size) is { } existingRelease)
            await ClearReleaseForVideo(video, existingRelease);
    }

    private async Task ClearReleaseForVideo(IVideo video, StoredReleaseInfo releaseInfo)
    {
        if (video is VideoLocal videoLocal)
        {
            videoLocal.DateTimeImported = null;
            videoRepository.Save(videoLocal);
        }

        var xrefs = xrefRepository.GetByEd2k(video.Hashes.ED2K);
        xrefRepository.Delete(xrefs);

        releaseInfoRepository.Delete(releaseInfo);

        await ScheduleAnimeForRelease(xrefs);

        ReleaseDeleted?.Invoke(null, new() { Video = video, ReleaseInfo = releaseInfo });
    }

    private bool CheckCrossReferences(IVideo video, StoredReleaseInfo releaseInfo, out List<SVR_CrossRef_File_Episode> legacyXrefs)
    {
        legacyXrefs = [];

        var legacyOrder = 0;
        var embeddedXrefs = new List<EmbeddedCrossReference>();
        foreach (var xref in releaseInfo.CrossReferences.OfType<EmbeddedCrossReference>())
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

            if (xref.AnidbEpisodeID is <= 0)
                continue;

            // The provider doesn't know which anime the episode belongs to, so try to fix that.
            var animeID = xref.AnidbAnimeID;
            if (animeID is null or <= 0)
            {
                animeID = null;
                if (anidbEpisodeRepository.GetByEpisodeID(xref.AnidbEpisodeID) is { } episode)
                {
                    animeID = episode.AnimeID;
                }
                else if (udpConnection.IsBanned)
                {
                    logger.LogInformation("Could not get AnimeID for episode {EpisodeID}, but we're UDP banned, so deferring fetch to later!", xref.AnidbEpisodeID);
                }
                else
                {
                    logger.LogInformation("Could not get AnimeID for episode {EpisodeID}, downloading more info…", xref.AnidbEpisodeID);
                    try
                    {
                        var episodeResponse = requestFactory
                            .Create<RequestGetEpisode>(r => r.EpisodeID = xref.AnidbEpisodeID)
                            .Send();
                        animeID = episodeResponse.Response?.AnimeID;
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Could not get Episode Info for {EpisodeID}!", xref.AnidbEpisodeID);
                    }
                }
                if (animeID is not null)
                    xref.AnidbAnimeID = animeID;
                else
                    xref.AnidbAnimeID = null;
            }

            embeddedXrefs.Add(xref);
            legacyXrefs.Add(new()
            {
                Hash = video.Hashes.ED2K,
                AnimeID = animeID ?? 0,
                EpisodeID = xref.AnidbEpisodeID,
                Percentage = xref.PercentageEnd - xref.PercentageStart,
                EpisodeOrder = legacyOrder++,
                FileName = (video.Locations.FirstOrDefault(loc => loc.IsAvailable) ?? video.Locations.FirstOrDefault())?.FileName,
                FileSize = video.Size,
            });
        }

        if (embeddedXrefs.Count != releaseInfo.CrossReferences.Count)
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
                    groupBy.Any(xref => xref.AnidbEpisode is null)
            );
        foreach (var (animeID, missingEpisodes) in animeIDs)
        {
            var animeRecentlyUpdated = false;
            var update = anidbAnimeUpdateRepository.GetByAnimeID(animeID)!;
            if (!missingEpisodes && (DateTime.Now - update.UpdatedAt).TotalHours < _settings.AniDb.MinimumHoursToRedownloadAnimeInfo)
                animeRecentlyUpdated = true;

            // even if we are missing episode info, don't get data  more than once every `x` hours
            // this is to prevent banning
            var scheduler = await schedulerFactory.GetScheduler().ConfigureAwait(false);
            if (missingEpisodes)
            {
                logger.LogInformation("Queuing immediate GET for AniDB_Anime: {AnimeID}", animeID);

                // this should detect and handle a ban, which will leave Result null, and defer
                await scheduler.StartJobNow<GetAniDBAnimeJob>(c =>
                {
                    c.AnimeID = animeID;
                    c.ForceRefresh = true;
                    c.DownloadRelations = _settings.AutoGroupSeries || _settings.AniDb.DownloadRelatedAnime;
                    c.CreateSeriesEntry = true;
                }).ConfigureAwait(false);
            }
            else if (!animeRecentlyUpdated)
            {
                logger.LogInformation("Queuing GET for AniDB_Anime: {AnimeID}", animeID);

                // this should detect and handle a ban, which will leave Result null, and defer
                await scheduler.StartJob<GetAniDBAnimeJob>(c =>
                {
                    c.AnimeID = animeID;
                    c.ForceRefresh = true;
                    c.DownloadRelations = _settings.AutoGroupSeries || _settings.AniDb.DownloadRelatedAnime;
                    c.CreateSeriesEntry = true;
                }).ConfigureAwait(false);
            }
            else
            {
                await scheduler.StartJob<RefreshAnimeStatsJob>(b => b.AnimeID = animeID);
            }

            var tmdbShowXrefs = crossRefAnidbTmdbRepository.GetByAnidbAnimeID(animeID);
            foreach (var xref in tmdbShowXrefs)
                await scheduler.StartJob<UpdateTmdbShowJob>(job =>
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
            .ExceptBy([video.Hashes.ED2K], v => v.Hash)
            .Cast<IVideo>()
            .ToList();

        if (otherVideos.Count == 0)
            return;

        foreach (var user in userService.GetUsers())
        {
            var watchedVideo = otherVideos
                .FirstOrDefault(video => userDataService.GetVideoUserData(user.ID, video.ID)?.LastPlayedAt is not null);
            if (watchedVideo is null)
                continue;

            var watchedRecord = userDataService.GetVideoUserData(user.ID, watchedVideo.ID)!;
            userDataService.SaveVideoUserData(user, video, new(watchedRecord), UserDataSaveReason.VideoReImport);
        }
    }

    /// <summary>
    /// Gets a unique ID for a release provider generated from its class name.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns><see cref="Guid" />.</returns>
    internal static Guid GetID(IReleaseInfoProvider provider)
        => GetID(provider.GetType());

    /// <summary>
    /// Gets a unique ID for a release provider generated from its class name.
    /// </summary>
    /// <param name="providerType">The provider type.</param>
    /// <returns><see cref="Guid" />.</returns>
    internal static Guid GetID(Type providerType)
        => new(MD5.HashData(Encoding.Unicode.GetBytes(providerType.FullName!)));
}
