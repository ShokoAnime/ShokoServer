using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Events;
using Shoko.Abstractions.Video.Hashing;
using Shoko.Abstractions.Video.Release;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Models.Release;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.TMDB;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Providers.AniDB.Release;

/// <summary>
///    The built-in AniDB release provider, using the AniDB UDP API through the
///    internal UDP connection handler.
/// </summary>
/// <param name="logger">The logger.</param>
/// <param name="configurationProvider">The configuration provider.</param>
/// <param name="requestFactory">The request factory.</param>
/// <param name="connectionHandler">The connection handler.</param>
/// <param name="fileNameHashRepository">The file name hash repository.</param>
/// <param name="videoRepository">The video repository.</param>
/// <param name="anidbEpisodeRepository">The AniDB episode repository.</param>
/// <param name="anidbAnimeRepository">The AniDB anime repository.</param>
/// <param name="anidbAnimeUpdateRepository">The AniDB anime update repository.</param>
/// <param name="shokoSeriesRepository">The Shoko series repository.</param>
/// <param name="crossRefAnidbTmdbRepository">The AniDB↔TMDB show cross-reference repository.</param>
/// <param name="releaseInfoRepository">The stored release info repository.</param>
/// <param name="scheduler">The job scheduler.</param>
/// <param name="settingsProvider">The settings provider.</param>
/// <param name="serviceProvider">The service provider for lazy service resolution.</param>
public partial class AnidbReleaseProvider(
    ILogger<AnidbReleaseProvider> logger,
    ConfigurationProvider<AnidbReleaseProvider.AnidbReleaseProviderSettings> configurationProvider,
    IRequestFactory requestFactory,
    IUDPConnectionHandler connectionHandler,
    FileNameHashRepository fileNameHashRepository,
    VideoLocalRepository videoRepository,
    AniDB_EpisodeRepository anidbEpisodeRepository,
    AniDB_AnimeRepository anidbAnimeRepository,
    AniDB_AnimeUpdateRepository anidbAnimeUpdateRepository,
    AnimeSeriesRepository shokoSeriesRepository,
    CrossRef_AniDB_TMDB_ShowRepository crossRefAnidbTmdbRepository,
    StoredReleaseInfoRepository releaseInfoRepository,
    IQueueScheduler scheduler,
    ISettingsProvider settingsProvider,
    IServiceProvider serviceProvider
) : IReleaseInfoProvider<AnidbReleaseProvider.AnidbReleaseProviderSettings>
{
    /// <summary>
    /// Simple memory cache to prevent looking up the same file multiple times within half an hour.
    /// </summary>
    private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions()
    {
        ExpirationScanFrequency = TimeSpan.FromMinutes(25),
    });

    private readonly HashSet<int> _unknownEpisodeIDs = [];

    // Lazy to prevent circular DI: VideoReleaseService → AnidbReleaseProvider → IAnidbService → VideoReleaseService
    private IAnidbService? _anidbService;

    private IServerSettings Settings => settingsProvider.GetSettings();

    internal static readonly HashSet<string?> InvalidGroupNames = GetAniDBReleaseGroupJob.InvalidReleaseGroupNames;

    /// <summary>
    ///    Prefix for AniDB file URLs.
    /// </summary>
    public const string ReleasePrefix = "https://anidb.net/file/";

    public const string IdPrefix = "anidb://";

    /// <inheritdoc/>
    public string Name => "AniDB";

    /// <inheritdoc />
    public string Description => """
        The built-in AniDB release provider, using the AniDB UDP API through the
        internal UDP connection handler.
    """;

    /// <inheritdoc/>
    public Task<ReleaseInfo?> GetReleaseInfoForVideo(ReleaseInfoContext request, CancellationToken cancellationToken)
        => GetReleaseInfoById($"{IdPrefix}{request.Video.ED2K}+{request.Video.Size}", request.Video);

    /// <inheritdoc/>
    public Task<ReleaseInfo?> GetReleaseInfoById(string releaseId, CancellationToken cancellationToken)
        => GetReleaseInfoById(releaseId, null);

    /// <summary>
    ///    Gets the release info by ID. The ID should be a hash+size combination.
    /// </summary>
    /// <param name="releaseId">Release ID. Hash+Size.</param>
    /// <param name="video">Optional. A loaded video instance to use for some extra metadata to include in the release.</param>
    /// <returns>The release info, or null if not found.</returns>
    private async Task<ReleaseInfo?> GetReleaseInfoById(string releaseId, IVideo? video = null)
    {
        if (_memoryCache.TryGetValue(releaseId, out ReleaseInfo? releaseInfo))
            return releaseInfo;

        if (string.IsNullOrEmpty(releaseId) || !releaseId.StartsWith(IdPrefix))
            return null;

        releaseId = releaseId[IdPrefix.Length..];
        var (hash, fileSize) = releaseId.Split('+');
        if (string.IsNullOrEmpty(hash) || hash.Length != 32 || !long.TryParse(fileSize, out var size))
            return null;

        if (connectionHandler.IsBanned)
        {
            logger.LogInformation("Unable to lookup release for Hash={Hash} & Size={Size} due to being AniDB UDP banned.", hash, size);
            return null;
        }

        ResponseGetFile anidbFile;
        try
        {
            var response = await Task.Run(() => requestFactory.Create<RequestGetFile>(request => { request.Hash = hash; request.Size = size; }).Send());
            if (response?.Response is null)
            {
                logger.LogInformation("Unable to find a release for Hash={Hash} & Size={Size} at AniDB.", hash, size);
                return null;
            }

            anidbFile = response.Response;
        }
        catch (NotLoggedInException ex)
        {
            logger.LogError(ex, "Unable to lookup release for Hash={Hash} & Size={Size} due to being AniDB UDP banned.", hash, size);
            return null;
        }
        catch (AniDBBannedException ex)
        {
            logger.LogError(ex, "Unable to lookup release for Hash={Hash} & Size={Size} due to being AniDB UDP banned.", hash, size);
            return null;
        }

        video ??= videoRepository.GetByEd2kAndSize(hash, size);

        var settings = configurationProvider.Load();
        var creditless = (bool?)null;
        if (settings.CheckCreditless)
        {
            var regex = GeneratedCreditlessRegex();
            // Check anidb's remote file name
            if (!string.IsNullOrEmpty(anidbFile.Filename) && regex.IsMatch(anidbFile.Filename))
                creditless = true;
            // then check any locations for the video if the video is available
            else if (video?.Files is { Count: > 0 } locations && locations.Any(x => regex.IsMatch(x.FileName)))
                creditless = true;
            // and as a last ditch effort check locally known file names for the hash
            else if (fileNameHashRepository.GetByHash(hash).Where(x => x.FileSize == size).Select(x => x.FileName).ToList() is { Count: > 0 } knownFileNames && knownFileNames.Any(regex.IsMatch))
                creditless = true;
            else
                creditless = false;
        }

        releaseInfo = new ReleaseInfo()
        {
            ID = IdPrefix + releaseId,
            ReleaseURI = $"{ReleasePrefix}{anidbFile.FileID}",
            IsPublic = true,
            Version = anidbFile.Version,
            Comment = anidbFile.Description,
            OriginalFilename = anidbFile.Filename,
            IsCensored = anidbFile.Censored,
            IsChaptered = anidbFile.Chaptered,
            IsCreditless = creditless,
            IsCorrupted = anidbFile.Deprecated,
            Source = anidbFile.Source switch
            {
                GetFile_Source.TV => ReleaseSource.TV,
                GetFile_Source.DTV => ReleaseSource.TV,
                GetFile_Source.HDTV => ReleaseSource.TV,
                GetFile_Source.DVD => ReleaseSource.DVD,
                GetFile_Source.HKDVD => ReleaseSource.DVD,
                GetFile_Source.HDDVD => ReleaseSource.DVD,
                GetFile_Source.VHS => ReleaseSource.VHS,
                GetFile_Source.Camcorder => ReleaseSource.Camera,
                GetFile_Source.VCD => ReleaseSource.VCD,
                GetFile_Source.SVCD => ReleaseSource.VCD,
                GetFile_Source.LaserDisc => ReleaseSource.LaserDisc,
                GetFile_Source.BluRay => ReleaseSource.BluRay,
                GetFile_Source.Web => ReleaseSource.Web,
                GetFile_Source.Film8mm => ReleaseSource.Film,
                GetFile_Source.Film16mm => ReleaseSource.Film,
                GetFile_Source.Film35mm => ReleaseSource.Film,
                _ => ReleaseSource.Unknown,
            },
            Group = new()
            {
                ID = anidbFile.GroupID?.ToString() ?? string.Empty,
                Source = "AniDB",
                Name = string.IsNullOrEmpty(anidbFile.GroupName) ? string.Empty : anidbFile.GroupName,
                ShortName = string.IsNullOrEmpty(anidbFile.GroupShortName) ? string.Empty : anidbFile.GroupShortName,
            },
            MediaInfo = new()
            {
                AudioLanguages = anidbFile.AudioLanguages
                    .Select(a => a.GetTitleLanguage())
                    .ToList(),
                SubtitleLanguages = anidbFile.SubtitleLanguages
                    .Select(a => a.GetTitleLanguage())
                    .ToList(),
            },
            FileSize = size,
            Hashes = [
                    new() { Type = "ED2K", Value = hash },
                    ..settings.StoreHashes
                        ? video?.Hashes.Select(x => new HashDigest() { Type = x.Type, Value = x.Value, Metadata = x.Metadata }) ?? []
                        : [],
                ],
            ReleasedAt = anidbFile.ReleasedAt,
            CreatedAt = DateTime.Now,
        };

        // These percentages will probably be wrong, but we can tolerate that for now
        // until a better solution to get more accurate readings for the start/end ranges
        // is found.
        var offset = 0;
        foreach (var xref in anidbFile.EpisodeIDs)
        {
            releaseInfo.CrossReferences.Add(new()
            {
                AnidbAnimeID = anidbFile.AnimeID,
                AnidbEpisodeID = xref.EpisodeID,
                PercentageStart = xref.Percentage < 100 ? offset : 0,
                PercentageEnd = xref.Percentage < 100 ? offset + xref.Percentage : 100,
            });
            if (xref.Percentage < 100)
                offset += xref.Percentage;
        }
        foreach (var xref in anidbFile.OtherEpisodes)
        {
            releaseInfo.CrossReferences.Add(new()
            {
                AnidbEpisodeID = xref.EpisodeID,
                PercentageStart = xref.Percentage < 100 ? offset : 0,
                PercentageEnd = xref.Percentage < 100 ? offset + xref.Percentage : 100,
            });
            if (xref.Percentage < 100)
                offset += xref.Percentage;
        }

        logger.LogInformation("Found a release for Hash={Hash} & Size={Size} at AniDB!", hash, size);
        _memoryCache.Set(releaseId, releaseInfo, TimeSpan.FromMinutes(30));
        return releaseInfo;
    }

    /// <inheritdoc/>
    public async Task PrepareForSave(IVideo video, ReleaseInfo releaseInfo)
    {
        // Fill in missing AnidbAnimeIDs for any cross-references where the provider didn't supply them.
        var toRemove = new List<ReleaseVideoCrossReference>();
        foreach (var xref in releaseInfo.CrossReferences.Where(x => x.AnidbAnimeID is null or 0))
        {
            if (_unknownEpisodeIDs.Contains(xref.AnidbEpisodeID))
            {
                logger.LogError("Unknown episode id: {EpisodeID}!", xref.AnidbEpisodeID);
                toRemove.Add(xref);
                continue;
            }

            if (anidbEpisodeRepository.GetByEpisodeID(xref.AnidbEpisodeID) is { } episode)
            {
                xref.AnidbAnimeID = episode.AnimeID;
            }
            else if (connectionHandler.IsBanned)
            {
                logger.LogInformation("Could not get AnimeID for episode {EpisodeID}, but we're UDP banned, so deferring fetch to later!", xref.AnidbEpisodeID);
            }
            else
            {
                logger.LogInformation("Could not get AnimeID for episode {EpisodeID}, downloading more info…", xref.AnidbEpisodeID);
                try
                {
                    var episodeResponse = requestFactory.Create<RequestGetEpisode>(r => r.EpisodeID = xref.AnidbEpisodeID).Send();
                    if (episodeResponse.Code is UDPReturnCode.NO_SUCH_EPISODE)
                    {
                        logger.LogError("Unknown episode with id {EpisodeID}!", xref.AnidbEpisodeID);
                        _unknownEpisodeIDs.Add(xref.AnidbEpisodeID);
                        toRemove.Add(xref);
                        continue;
                    }
                    xref.AnidbAnimeID = episodeResponse.Response?.AnimeID;
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Could not get Episode Info for {EpisodeID}!", xref.AnidbEpisodeID);
                }
            }
        }
        foreach (var xref in toRemove)
            releaseInfo.CrossReferences.Remove(xref);

        // Validate/fetch group info when names are missing or invalid.
        if (releaseInfo.Group is not null)
            await PrepareGroupForSave(releaseInfo);
    }

    private async Task PrepareGroupForSave(ReleaseInfo releaseInfo)
    {
        var group = releaseInfo.Group!;
        if (string.IsNullOrEmpty(group.ID) || string.IsNullOrEmpty(group.Source))
        {
            releaseInfo.Group = null;
            return;
        }

        if (
            !InvalidGroupNames.Contains(group.Name) &&
            !InvalidGroupNames.Contains(group.ShortName) &&
            !string.IsNullOrEmpty(group.Name) &&
            !string.IsNullOrEmpty(group.ShortName)
        )
            return;

        // Re-use names from another stored release by the same group to avoid a UDP call.
        var existingReleasesForGroup = releaseInfoRepository.GetByGroupAndProviderIDs(group.ID, group.Source)
            .Where(rI => !string.IsNullOrEmpty(rI.GroupName) && !string.IsNullOrEmpty(rI.GroupShortName))
            .OrderByDescending(rI => rI.LastUpdatedAt)
            .ToList();
        if (existingReleasesForGroup.Count > 0)
        {
            group.Name = existingReleasesForGroup[0].GroupName!;
            group.ShortName = existingReleasesForGroup[0].GroupShortName!;
            return;
        }

        // Only AniDB groups have numeric IDs that can be fetched via UDP.
        if (group.Source is not "AniDB" || !int.TryParse(group.ID, out var groupID) || groupID <= 0)
        {
            releaseInfo.Group = null;
            return;
        }

        try
        {
            var response = requestFactory.Create<RequestReleaseGroup>(r => r.ReleaseGroupID = groupID).Send();
            if (
                response.Response is not null &&
                !string.IsNullOrEmpty(response.Response.Name) &&
                !string.IsNullOrEmpty(response.Response.ShortName) &&
                !InvalidGroupNames.Contains(response.Response.Name) &&
                !InvalidGroupNames.Contains(response.Response.ShortName)
            )
            {
                group.Name = response.Response.Name;
                group.ShortName = response.Response.ShortName;
            }
            else
            {
                releaseInfo.Group = null;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not get release group info from AniDB for GroupID={GroupID}!", groupID);
            group.Name = null!;
            group.ShortName = null!;
            // Schedule a dedicated job to fetch group info later.
            await scheduler.RunAfterCurrent<GetAniDBReleaseGroupJob>(c => c.GroupID = groupID);
        }
    }

    /// <inheritdoc/>
    public async Task OnReleaseSaved(IVideo? video, IReleaseInfo savedRelease, IReadOnlyList<IVideoCrossReference> xrefs)
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
        if (Settings.AutoGroupSeries || Settings.AniDb.DownloadRelatedAnime)
            refreshMethod |= AnidbRefreshMethod.DownloadRelations;

        foreach (var (animeID, missingEpisodes) in animeIDs)
        {
            var animeRecentlyUpdated = false;
            var update = anidbAnimeUpdateRepository.GetByAnimeID(animeID)!;
            if (!missingEpisodes && (DateTime.Now - update.UpdatedAt).TotalHours < Settings.AniDb.MinimumHoursToRedownloadAnimeInfo)
                animeRecentlyUpdated = true;

            if (missingEpisodes)
            {
                logger.LogInformation("Queuing immediate GET for AniDB_Anime: {AnimeID}", animeID);
                _anidbService ??= serviceProvider.GetRequiredService<IAnidbService>();
                await _anidbService.ScheduleRefreshOfAnimeByID(animeID, refreshMethod, prioritize: true);
                await scheduler.RunAfterCurrent<RefreshAnimeStatsJob>(b => b.AnimeID = animeID);
            }
            else if (!animeRecentlyUpdated)
            {
                logger.LogInformation("Queuing GET for AniDB_Anime: {AnimeID}", animeID);
                _anidbService ??= serviceProvider.GetRequiredService<IAnidbService>();
                await _anidbService.ScheduleRefreshOfAnimeByID(animeID, refreshMethod);
                await scheduler.RunAfterCurrent<RefreshAnimeStatsJob>(b => b.AnimeID = animeID);
            }
            else
            {
                await scheduler.RunAfterCurrent<RefreshAnimeStatsJob>(b => b.AnimeID = animeID);
            }

            var tmdbShowXrefs = crossRefAnidbTmdbRepository.GetByAnidbAnimeID(animeID);
            foreach (var xref in tmdbShowXrefs)
                await scheduler.RunAfterCurrent<UpdateTmdbShowJob>(job =>
                {
                    job.TmdbShowID = xref.TmdbShowID;
                    job.DownloadImages = true;
                });
        }
    }

    /// <inheritdoc/>
    public async Task OnReleaseCleared(IVideo? video, IReleaseInfo clearedRelease, IReleaseInfo? replacingRelease)
    {
        if (Settings.AniDb.MyList_DeleteType is AniDBFileDeleteType.DeleteLocalOnly)
        {
            var hash = clearedRelease.Hashes?.FirstOrDefault(h => h.Type == "ED2K")?.Value ?? string.Empty;
            logger.LogInformation("Keeping physical file and AniDB MyList entry, deleting from local DB: Hash: {Hash}", hash);
            return;
        }

        // When replacing with the exact same AniDB file, the MyList entry stays.
        if (replacingRelease?.ReleaseURI is not null && replacingRelease.ReleaseURI == clearedRelease.ReleaseURI)
            return;

        if (clearedRelease is { ReleaseURI: not null } && clearedRelease.ReleaseURI.StartsWith(ReleasePrefix))
        {
            var hash = clearedRelease.Hashes?.FirstOrDefault(h => h.Type == "ED2K")?.Value ?? string.Empty;
            await scheduler.StartJob<DeleteFileFromMyListJob>(c =>
            {
                c.Hash = hash;
                c.FileSize = clearedRelease.FileSize ?? 0;
            });
        }
        else
        {
            foreach (var xref in clearedRelease.CrossReferences)
            {
                if (xref.AnidbAnimeID is null or 0)
                    continue;

                var anidbEpisode = anidbEpisodeRepository.GetByEpisodeID(xref.AnidbEpisodeID);
                if (anidbEpisode is null)
                    continue;

                await scheduler.StartJob<DeleteFileFromMyListJob>(c =>
                {
                    c.AnimeID = xref.AnidbAnimeID!.Value;
                    c.EpisodeType = anidbEpisode.EpisodeType;
                    c.EpisodeNumber = anidbEpisode.EpisodeNumber;
                });
            }
        }
    }

    /// <inheritdoc/>
    public async Task OnSearchCompleted(VideoReleaseSearchCompletedEventArgs args)
    {
        if (args.IsCancelled) return;
        if (!settingsProvider.GetSettings().AniDb.MyList_AddFiles) return;
        await scheduler.StartJob<AddFileToMyListJob>(c => { c.Hash = args.Video.ED2K; c.ReadStates = true; });
    }

    /// <inheritdoc/>
    public TimeSpan? GetRescanDelay(IReleaseInfo existingInfo, IReleaseMatchAttempt lastAttempt)
    {
        if (existingInfo.IsPublic == false) return null;
        if (lastAttempt.ProviderName != Name) return null;
        if (existingInfo.Source != ReleaseSource.Unknown && existingInfo.MediaInfo is not null)
            return null;
        var settings = configurationProvider.Load();
        if (lastAttempt.AttemptCount >= settings.RescanDelayHours.Length) return null;
        return TimeSpan.FromHours(settings.RescanDelayHours[lastAttempt.AttemptCount]);
    }

    [GeneratedRegex(@"(?:(?<![a-z0-9])(?:nc|creditless)[\s_.]*(?:ed|op)(?![a-z]))(?:[\s_.]*(?:\d+(?!\d*p)))?", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ECMAScript)]
    private static partial Regex GeneratedCreditlessRegex();

    /// <summary>
    /// Helper to get the generated regex outside this class, since calling the
    /// method above seems to be causing problems due to the way it's compiled
    /// and this being a partial class.
    /// </summary>
    internal static Regex CreditlessRegex => GeneratedCreditlessRegex();

    /// <summary>
    /// Configure some aspects of the built-in AniDB release provider.
    /// </summary>
    [Display(Name = "Built-in AniDB Release Provider")]
    public class AnidbReleaseProviderSettings : IReleaseInfoProviderConfiguration, INewtonsoftJsonConfiguration
    {
        /// <summary>
        /// If set to true, hashes stored in the database will be included in
        /// the provided release info.
        /// </summary>
        [Display(Name = "Store existing hashes")]
        [DefaultValue(true)]
        public bool StoreHashes { get; set; } = true;

        /// <summary>
        /// If set to true, the release will be checked if it is creditless by
        /// checking all known file names, both local and remote for a 'NC' or
        /// 'creditless' tag.
        /// </summary>
        [Display(Name = "Check if release is creditless")]
        [DefaultValue(true)]
        public bool CheckCreditless { get; set; } = true;

        /// <summary>
        /// Delay in hours before each successive rescan attempt for releases
        /// with missing info (unknown source, or missing audio/subtitle
        /// languages). The number of entries also controls the maximum number
        /// of rescan attempts — once exhausted, no further rescans are
        /// scheduled. Default: 5 attempts at 6 h, 1 d, 3 d, 1 w, 3 mo.
        /// </summary>
        [Display(Name = "Rescan backoff schedule (hours per attempt)")]
        public int[] RescanDelayHours { get; set; } = [6, 24, 72, 168, 2160];
    }
}
