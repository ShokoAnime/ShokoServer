
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Plugin.Abstractions.Services;
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
    DatabaseReleaseInfoRepository releaseInfoRepository,
    AniDB_EpisodeRepository anidbEpisodeRepository,
    AniDB_AnimeRepository anidbAnimeRepository,
    AniDB_AnimeUpdateRepository anidbAnimeUpdateRepository,
    AnimeSeriesRepository shokoSeriesRepository,
    CrossRef_AniDB_TMDB_ShowRepository crossRefAnidbTmdbRepository,
    CrossRef_File_EpisodeRepository xrefRepository
) : IVideoReleaseService
{
    private IServerSettings _settings => settingsProvider.GetSettings();

    private Dictionary<string, IReleaseInfoProvider>? _releaseInfoProviders = null;

    public event EventHandler<VideoReleaseEventArgs>? VideoReleaseSaved;

    public event EventHandler<VideoReleaseEventArgs>? VideoReleaseDeleted;

    public void AddProviders(IEnumerable<IReleaseInfoProvider> providers)
    {
        if (_releaseInfoProviders is not null)
            return;

        _releaseInfoProviders = providers
            .DistinctBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<ReleaseInfoProviderInfo> GetAvailableProviders()
    {
        if (_releaseInfoProviders is null)
            yield break;

        var settings = settingsProvider.GetSettings().Plugins.ReleaseProvider;
        var order = settings.Priority;
        var enabled = settings.Enabled;
        var orderedProviders = _releaseInfoProviders.Values
            .OrderBy(p => order.IndexOf(p.Name) is -1)
            .ThenBy(p => order.IndexOf(p.Name))
            .ThenBy(p => p.Name)
            .Select((provider, index) => (provider, index));
        foreach (var (provider, index) in orderedProviders)
        {
            yield return new()
            {
                Provider = provider,
                Enabled = enabled.TryGetValue(provider.Name, out var isEnabled) && isEnabled,
                Priority = index,
            };
        }
    }

    public IReleaseInfoProvider? GetProviderByID(string providerID)
        => _releaseInfoProviders?.TryGetValue(providerID, out var provider) ?? false ? provider : null;

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
                else if (wantedIndex > existingIndex && wantedIndex > 0)
                    existingProviders.Insert(wantedIndex - 1, pI);
                else
                    existingProviders.Insert(wantedIndex, pI);
            }
        }

        var settings = settingsProvider.GetSettings();
        settings.Plugins.ReleaseProvider.Priority = existingProviders
            .Select(pI => pI.Provider.Name)
            .ToList();
        settings.Plugins.ReleaseProvider.Enabled = existingProviders
            .ToDictionary(p => p.Provider.Name, p => p.Enabled);
        settingsProvider.SaveSettings(settings);
    }

    public IReleaseInfo? GetCurrentReleaseForVideo(IVideo video)
        => releaseInfoRepository.GetByEd2kAndFileSize(video.Hashes.ED2K, video.Size);

    public async Task<IReleaseInfo?> FindReleaseForVideo(IVideo video, bool saveRelease = true, CancellationToken cancellationToken = default)
    {
        IReleaseInfo? releaseInfo = null;
        foreach (var providerInfo in GetAvailableProviders())
        {
            if (!providerInfo.Enabled)
                continue;

            var provider = providerInfo.Provider;
            var release = await provider.GetReleaseInfoForVideo(video, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (release is null || release.CrossReferences.Count < 1)
                continue;

            releaseInfo = new ReleaseInfoWithProvider(release, provider.Name);
            break;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!saveRelease || releaseInfo is null)
            return releaseInfo;

        return await SaveReleaseForVideo(video, releaseInfo);
    }

    public Task<IReleaseInfo> SaveReleaseForVideo(IVideo video, ReleaseInfo release, string providerName = "User")
        => SaveReleaseForVideo(video, new ReleaseInfoWithProvider(release, providerName));

    public async Task<IReleaseInfo> SaveReleaseForVideo(IVideo video, IReleaseInfo release)
    {
        if (release.CrossReferences.Count < 1)
            throw new InvalidOperationException("Release must have at least one valid cross reference.");

        var releaseInfo = new DatabaseReleaseInfo(video, release);
        if (!CheckCrossReferences(video, releaseInfo, out var legacyXrefs))
            throw new InvalidOperationException($"Release have {release.CrossReferences.Count - legacyXrefs.Count} invalid cross reference(s).");

        var missingGroupId = CheckReleaseGroup(releaseInfo);
        if (releaseInfoRepository.GetByEd2kAndFileSize(video.Hashes.ED2K, video.Size) is { } existingRelease)
        {
            // If the new release info is **EXACTLY** the same as the existing one, then just return the existing one.
            if (existingRelease == releaseInfo)
            {
                existingRelease.LastUpdatedAt = DateTime.Now;
                releaseInfoRepository.Save(existingRelease);
                return existingRelease;
            }

            await ClearReleaseForVideo(video, existingRelease);
        }

        releaseInfo.LastUpdatedAt = DateTime.Now;
        releaseInfoRepository.Save(releaseInfo);
        xrefRepository.Save(legacyXrefs);

        // Mark the video as imported if needed.
        if (video is SVR_VideoLocal videoLocal && videoLocal.DateTimeImported is null)
        {
            videoLocal.DateTimeImported = DateTime.Now;
            videoRepository.Save(videoLocal);
        }

        // Schedule the release group to be fetched if needed.
        if (missingGroupId is not null)
        {
            var scheduler = await schedulerFactory.GetScheduler();
            await scheduler.StartJob<GetAniDBReleaseGroupJob>(c => c.GroupID = missingGroupId.Value);
        }

        await ScheduleAnimeForRelease(legacyXrefs);

        SetWatchedStateIfNeeded(video, releaseInfo);

        VideoReleaseSaved?.Invoke(null, new(video, releaseInfo));

        return releaseInfo;
    }

    public async Task<bool> ClearReleaseForVideo(IVideo video)
    {
        var existingRelease = releaseInfoRepository.GetByEd2kAndFileSize(video.Hashes.ED2K, video.Size);
        if (existingRelease is null)
            return true;

        return await ClearReleaseForVideo(video, existingRelease);
    }

    private Task<bool> ClearReleaseForVideo(IVideo video, DatabaseReleaseInfo releaseInfo)
    {
        if (video is SVR_VideoLocal videoLocal)
        {
            videoLocal.DateTimeImported = null;
            videoRepository.Save(videoLocal);
        }

        var xrefs = xrefRepository.GetByEd2k(video.Hashes.ED2K);
        xrefRepository.Delete(xrefs);

        releaseInfoRepository.Delete(releaseInfo);

        VideoReleaseDeleted?.Invoke(null, new(video, releaseInfo));

        return Task.FromResult(true);
    }

    private bool CheckCrossReferences(IVideo video, DatabaseReleaseInfo releaseInfo, out List<SVR_CrossRef_File_Episode> legacyXrefs)
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
                CrossRefSource = releaseInfo.ProviderID.GetHashCode(),
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

    private int? CheckReleaseGroup(DatabaseReleaseInfo releaseInfo)
    {
        if (string.IsNullOrEmpty(releaseInfo.GroupID) || string.IsNullOrEmpty(releaseInfo.GroupProviderID))
        {
            releaseInfo.GroupID = null;
            releaseInfo.GroupProviderID = null;
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
        var existingReleasesForGroup = releaseInfoRepository.GetByGroupAndProviderIDs(releaseInfo.GroupID, releaseInfo.GroupProviderID)
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
        if (releaseInfo.GroupProviderID is not "AniDB" || !int.TryParse(releaseInfo.GroupID, out var groupID) || groupID <= 0)
        {
            releaseInfo.GroupID = null;
            releaseInfo.GroupProviderID = null;
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
                    releaseInfo.GroupProviderID = null;
                    releaseInfo.GroupName = null;
                    releaseInfo.GroupShortName = null;
                }
            }
            else
            {
                releaseInfo.GroupID = null;
                releaseInfo.GroupProviderID = null;
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
}
