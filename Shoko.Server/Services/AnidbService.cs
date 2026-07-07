using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Bulkhead;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Events;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.AniDB.Embedded;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Tasks;
using Shoko.Server.Utilities;

using CreatorType = Shoko.Server.Providers.AniDB.CreatorType;
using EpisodeType = Shoko.Abstractions.Metadata.Enums.EpisodeType;
using RelationType = Shoko.Abstractions.Metadata.Enums.RelationType;

namespace Shoko.Server.Services;

public class AnidbService : IAnidbService, IAnidbAvdumpService
{
    private readonly ILogger<AnidbService> _logger;

    private readonly IServiceProvider _serviceProvider;

    private readonly ISettingsProvider _settingsProvider;

    private readonly IRequestFactory _requestFactory;

    private readonly IQueueScheduler _scheduler;

    private readonly IVideoRelocationService _relocationService;

    private readonly IUDPConnectionHandler _udpConnectionHandler;

    private readonly IHttpConnectionHandler _httpConnectionHandler;

    private readonly HttpXmlUtils _xmlUtils;

    private readonly HttpAnimeParser _httpParser;

    private readonly AniDBTitleHelper _titleHelper;

    // Lazy init. to prevent circular dependency.
    private AnimeCreator? _animeCreator;

    private readonly AnimeGroupCreator _animeGroupCreator;

    // Lazy init. to prevent circular dependency.
    private AnimeSeriesService? _seriesService;

    private readonly AniDB_AnimeRepository _anidbAnimeRepository;

    private readonly AniDB_AnimeUpdateRepository _anidbAnimeUpdateRepository;

    private readonly AniDB_TagRepository _anidbTagRepository;

    private readonly AniDB_Anime_TitleRepository _anidbAnimeTitleRepository;

    private readonly AniDB_Anime_TagRepository _anidbAnimeTagRepository;

    private readonly AniDB_EpisodeRepository _anidbEpisodeRepository;

    private readonly AniDB_Episode_TitleRepository _anidbEpisodeTitleRepository;

    private readonly AniDB_Anime_CharacterRepository _anidbAnimeCharacterRepository;

    private readonly AniDB_CharacterRepository _anidbCharacterRepository;

    private readonly AniDB_Anime_Character_CreatorRepository _anidbAnimeCharacterCreatorRepository;

    private readonly AniDB_Anime_StaffRepository _anidbAnimeStaffRepository;

    private readonly AniDB_CreatorRepository _anidbCreatorRepository;

    private readonly AnimeSeriesRepository _seriesRepository;

    private readonly CrossRef_File_EpisodeRepository _crossReferenceRepository;

    private readonly StoredReleaseInfoRepository _storedReleaseInfoRepository;

    private readonly ShokoImage_EntityRepository _shokoImageXrefRepository;

    private readonly AniDB_Anime_RelationRepository _anidbAnimeRelationRepository;

    private readonly AsyncBulkheadPolicy<AniDB_Anime?> _bulkheadPolicy;

    private readonly IImageManager _imageManager;

    private readonly KeyedEntityLockHelper _entityLock;

    private readonly ISupplementaryMetadataService _supplementaryMetadataService;

    public AnidbService(
        ILogger<AnidbService> logger,
        IServiceProvider serviceProvider,
        ISettingsProvider settingsProvider,
        IRequestFactory requestFactory,
        IQueueScheduler scheduler,
        IVideoRelocationService relocationService,
        IUDPConnectionHandler udpConnectionHandler,
        IHttpConnectionHandler httpConnectionHandler,
        HttpXmlUtils xmlUtils,
        HttpAnimeParser httpParser,
        AniDBTitleHelper titleHelper,
        AnimeGroupCreator animeGroupCreator,
        AniDB_AnimeRepository anidbAnimeRepository,
        AniDB_AnimeUpdateRepository anidbAnimeUpdateRepository,
        AniDB_TagRepository anidbTagRepository,
        AniDB_Anime_TitleRepository anidbAnimeTitleRepository,
        AniDB_Anime_TagRepository anidbAnimeTagRepository,
        AniDB_EpisodeRepository anidbEpisodeRepository,
        AniDB_Episode_TitleRepository anidbEpisodeTitleRepository,
        AniDB_Anime_CharacterRepository anidbAnimeCharacterRepository,
        AniDB_CharacterRepository anidbCharacterRepository,
        AniDB_Anime_Character_CreatorRepository anidbAnimeCharacterCreatorRepository,
        AniDB_Anime_StaffRepository anidbAnimeStaffRepository,
        AniDB_CreatorRepository anidbCreatorRepository,
        AnimeSeriesRepository seriesRepository,
        CrossRef_File_EpisodeRepository crossReferenceRepository,
        StoredReleaseInfoRepository storedReleaseInfoRepository,
        ShokoImage_EntityRepository shokoImageXrefRepository,
        AniDB_Anime_RelationRepository anidbAnimeRelationRepository,
        IImageManager imageManager,
        ISupplementaryMetadataService supplementaryMetadataService
    )
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _settingsProvider = settingsProvider;
        _requestFactory = requestFactory;
        _scheduler = scheduler;
        _relocationService = relocationService;
        _serviceProvider = serviceProvider;
        _udpConnectionHandler = udpConnectionHandler;
        _httpConnectionHandler = httpConnectionHandler;
        _xmlUtils = xmlUtils;
        _httpParser = httpParser;
        _titleHelper = titleHelper;
        _animeGroupCreator = animeGroupCreator;
        _anidbAnimeRepository = anidbAnimeRepository;
        _anidbAnimeUpdateRepository = anidbAnimeUpdateRepository;
        _anidbTagRepository = anidbTagRepository;
        _anidbAnimeTitleRepository = anidbAnimeTitleRepository;
        _anidbAnimeTagRepository = anidbAnimeTagRepository;
        _anidbEpisodeRepository = anidbEpisodeRepository;
        _anidbEpisodeTitleRepository = anidbEpisodeTitleRepository;
        _anidbAnimeCharacterRepository = anidbAnimeCharacterRepository;
        _anidbCharacterRepository = anidbCharacterRepository;
        _anidbAnimeCharacterCreatorRepository = anidbAnimeCharacterCreatorRepository;
        _anidbAnimeStaffRepository = anidbAnimeStaffRepository;
        _anidbCreatorRepository = anidbCreatorRepository;
        _seriesRepository = seriesRepository;
        _crossReferenceRepository = crossReferenceRepository;
        _storedReleaseInfoRepository = storedReleaseInfoRepository;
        _shokoImageXrefRepository = shokoImageXrefRepository;
        _anidbAnimeRelationRepository = anidbAnimeRelationRepository;
        _imageManager = imageManager;
        _supplementaryMetadataService = supplementaryMetadataService;
        _entityLock = new(logger);
        _bulkheadPolicy = Policy.BulkheadAsync<AniDB_Anime?>(1, int.MaxValue);

        var now = DateTime.UnixEpoch.ToUniversalTime();
        LastHttpBanEventArgs = new()
        {
            Type = AnidbBanType.HTTP,
            OccurredAt = now,
            ExpiresAt = now,
        };
        LastUdpBanEventArgs = new()
        {
            Type = AnidbBanType.UDP,
            OccurredAt = now,
            ExpiresAt = now,
        };

        _udpConnectionHandler.BanExpired += OnAnidbBanExpired;
        _udpConnectionHandler.BanOccurred += OnAnidbBanOccurred;
        _httpConnectionHandler.BanExpired += OnAnidbBanExpired;
        _httpConnectionHandler.BanOccurred += OnAnidbBanOccurred;
        ShokoEventHandler.Instance.AvdumpEvent += OnAVDumpEvent;
    }

    ~AnidbService()
    {
        _udpConnectionHandler.BanExpired -= OnAnidbBanExpired;
        _udpConnectionHandler.BanOccurred -= OnAnidbBanOccurred;
        _httpConnectionHandler.BanExpired -= OnAnidbBanExpired;
        _httpConnectionHandler.BanOccurred -= OnAnidbBanOccurred;
        ShokoEventHandler.Instance.AvdumpEvent -= OnAVDumpEvent;
    }

    #region Banned Status

    /// <inheritdoc/>
    public event EventHandler<AnidbBanOccurredEventArgs>? BanOccurred;

    /// <inheritdoc/>
    public event EventHandler<AnidbBanOccurredEventArgs>? BanExpired;

    /// <inheritdoc/>
    public bool IsAnidbHttpBanned => _httpConnectionHandler.IsBanned;

    /// <inheritdoc/>
    public bool IsAnidbUdpBanned => _udpConnectionHandler.IsBanned;

    /// <inheritdoc/>
    public bool IsAnidbUdpReachable => _udpConnectionHandler.IsAlive && _udpConnectionHandler.IsNetworkAvailable;

    /// <inheritdoc/>
    public AnidbBanOccurredEventArgs LastHttpBanEventArgs { get; private set; }

    /// <inheritdoc/>
    public AnidbBanOccurredEventArgs LastUdpBanEventArgs { get; private set; }

    public void OnAnidbBanOccurred(object? sender, AnidbBanOccurredEventArgs eventArgs)
    {
        switch (eventArgs.Type)
        {
            case AnidbBanType.UDP:
                LastUdpBanEventArgs = eventArgs;
                break;

            case AnidbBanType.HTTP:
                LastHttpBanEventArgs = eventArgs;
                break;

            default:
                return;
        }

        BanOccurred?.Invoke(this, eventArgs);
    }

    public void OnAnidbBanExpired(object? sender, AnidbBanOccurredEventArgs eventArgs)
    {
        switch (eventArgs.Type)
        {
            case AnidbBanType.UDP:
                LastUdpBanEventArgs = eventArgs;
                break;

            case AnidbBanType.HTTP:
                LastHttpBanEventArgs = eventArgs;
                break;

            default:
                return;
        }

        BanExpired?.Invoke(this, eventArgs);
    }

    #endregion

    #region URLs

    public string? AnidbHttpApiBaseUrlOverride
    {
        get => _settingsProvider.GetSettings().AniDb.HTTPServerUrl is { Length: > 0 } url && !string.Equals(url, Constants.AnidbHttpApiUrl)
            ? url
            : null;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, Constants.AnidbHttpApiUrl))
                _settingsProvider.GetSettings().AniDb.HTTPServerUrl = Constants.AnidbHttpApiUrl;
            else
                _settingsProvider.GetSettings().AniDb.HTTPServerUrl = value;
            _settingsProvider.SaveSettings();
        }
    }

    public string? AnidbCdnBaseUrlOverride
    {
        get => _settingsProvider.GetSettings().AniDb.ImageCdnUrl is { Length: > 0 } url && !string.Equals(url, Constants.AnidbCdnUrl)
            ? url
            : null;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, Constants.AnidbCdnUrl))
                _settingsProvider.GetSettings().AniDb.ImageCdnUrl = null;
            else
                _settingsProvider.GetSettings().AniDb.ImageCdnUrl = value;
            _settingsProvider.SaveSettings();
        }
    }

    public string? AnidbTitleCacheUrlOverride
    {
        get => _settingsProvider.GetSettings().AniDb.TitleCacheUrl is { Length: > 0 } url && !string.Equals(url, Constants.AnidbTitleCacheUrl)
            ? url
            : null;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, Constants.AnidbTitleCacheUrl))
                _settingsProvider.GetSettings().AniDb.TitleCacheUrl = null;
            else
                _settingsProvider.GetSettings().AniDb.TitleCacheUrl = value;
            _settingsProvider.SaveSettings();
        }
    }

    #endregion

    #region "Remote" Search

    /// <inheritdoc/>
    public IReadOnlyList<IAnidbAnimeSearchResult> SearchAnime(string query, bool fuzzy)
        => _titleHelper.SearchTitle(query, fuzzy)
            .Select(a => new AbstractAnidbAnimeSearchResult(a, _anidbAnimeRepository, _seriesRepository))
            .ToList();

    /// <inheritdoc/>
    public IAnidbAnimeSearchResult? SearchAnimeByID(int anidbID)
        => _titleHelper.SearchAnimeID(anidbID) is { } result
            ? new AbstractAnidbAnimeSearchResult(new() { Result = result, Match = result.DefaultTitle.Value, ExactMatch = true }, _anidbAnimeRepository, _seriesRepository)
            : null;

    #endregion

    #region Refresh

    #region By AniDB Anime ID

    /// <inheritdoc/>
    public async Task<IAnidbAnime?> RefreshAnimeByID(int anidbAnimeID, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto, CancellationToken cancellationToken = default)
    {
        if (anidbAnimeID <= 0)
            return null;

        var anime = _anidbAnimeRepository.GetByAnimeID(anidbAnimeID);
        return await RefreshInternal(anidbAnimeID, anime, refreshMethod, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task ScheduleRefreshOfAnimeByID(int anidbAnimeID, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto, bool prioritize = false)
        => ScheduleRefreshInternal(anidbAnimeID, refreshMethod, prioritize);

    #endregion

    #region By AniDB Anime

    /// <inheritdoc/>
    public async Task<IAnidbAnime> RefreshAnime(IAnidbAnime anidbAnime, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(anidbAnime);

        return await RefreshInternal(anidbAnime.ID, anidbAnime, refreshMethod, cancellationToken).ConfigureAwait(false) ?? anidbAnime;
    }

    /// <inheritdoc/>
    public async Task ScheduleRefreshOfAnime(IAnidbAnime anidbAnime, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto, bool prioritize = false)
    {
        ArgumentNullException.ThrowIfNull(anidbAnime);

        await ScheduleRefreshInternal(anidbAnime.ID, refreshMethod, prioritize).ConfigureAwait(false);
    }

    #endregion

    #region Internals

    private async Task<IAnidbAnime?> RefreshInternal(int anidbAnimeID, IAnidbAnime? anidbAnime = null, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto, CancellationToken cancellationToken = default)
    {
        if (!refreshMethod.HasFlag(AnidbRefreshMethod.Cache) && !refreshMethod.HasFlag(AnidbRefreshMethod.Remote))
            return anidbAnime;

        try
        {
            var job = CreateJobDetails(anidbAnimeID, refreshMethod);
            var anime = await Process(job, cancellationToken).ConfigureAwait(false);
            return anime ?? anidbAnime;
        }
        catch (AniDBBannedException ex)
        {
            throw new AnidbHttpBannedException(ex) { ExpiresAt = ex.BanExpires?.ToUniversalTime() };
        }
    }

    private async Task ScheduleRefreshInternal(int anidbAnimeID, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto, bool prioritize = false)
    {
        if (!refreshMethod.HasFlag(AnidbRefreshMethod.Cache) && !refreshMethod.HasFlag(AnidbRefreshMethod.Remote))
            return;
        if (!refreshMethod.HasFlag(AnidbRefreshMethod.Cache))
        {
            await _scheduler.RunAfterCurrent<GetRemoteAniDBAnimeJob>(
                job => (job.AnimeID, job.RefreshMethod) = (anidbAnimeID, refreshMethod)
            ).ConfigureAwait(false);
        }
        else
        {
            await _scheduler.RunAfterCurrent<GetAniDBAnimeJob>(
                job => (job.AnimeID, job.RefreshMethod) = (anidbAnimeID, refreshMethod)
            ).ConfigureAwait(false);
        }
    }

    private AnidbJobDetails CreateJobDetails(int anidbAnimeID, AnidbRefreshMethod refreshMethod, int relationDepth = 0)
    {
        var job = new AnidbJobDetails { AnimeID = anidbAnimeID, RelDepth = relationDepth };

        // Use the defaults based on settings.
        if (refreshMethod is AnidbRefreshMethod.Auto)
        {
            var settings = _settingsProvider.GetSettings();
            job.DownloadRelations = settings.AutoGroupSeries || settings.AniDb.DownloadRelatedAnime;
            job.CreateSeriesEntry = settings.AniDb.AutomaticallyImportSeries;
        }
        // Toggle everything manually.
        else
        {
            job.UseCache = refreshMethod.HasFlag(AnidbRefreshMethod.Cache);
            job.UseRemote = refreshMethod.HasFlag(AnidbRefreshMethod.Remote);
            job.PreferCacheOverRemote = refreshMethod.HasFlag(AnidbRefreshMethod.PreferCacheOverRemote);
            job.DeferToRemoteIfUnsuccessful = refreshMethod.HasFlag(AnidbRefreshMethod.DeferToRemoteIfUnsuccessful);
            job.IgnoreTimeCheck = refreshMethod.HasFlag(AnidbRefreshMethod.IgnoreTimeCheck);
            job.IgnoreHttpBans = refreshMethod.HasFlag(AnidbRefreshMethod.IgnoreHttpBans);
            job.DownloadRelations = refreshMethod.HasFlag(AnidbRefreshMethod.DownloadRelations);
            job.CreateSeriesEntry = refreshMethod.HasFlag(AnidbRefreshMethod.CreateShokoSeries);
            job.SkipSupplementaryUpdate = refreshMethod.HasFlag(AnidbRefreshMethod.SkipSupplementaryUpdate);
        }

        return job;
    }

    public Task<AniDB_Anime?> Process(int anidbAnimeID, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Default, int relationDepth = 0, CancellationToken cancellationToken = default)
        => Process(CreateJobDetails(anidbAnimeID, refreshMethod, relationDepth), cancellationToken);

    private async Task<AniDB_Anime?> Process(AnidbJobDetails job, CancellationToken cancellationToken = default)
        => await _bulkheadPolicy.ExecuteAsync((ctx) => ProcessInternal(job, ctx), cancellationToken);

    private async Task<AniDB_Anime?> ProcessInternal(AnidbJobDetails job, CancellationToken cancellationToken)
    {
        using (await _entityLock.GetLockForEntityAsync(DataEntityType.Anime, job.AnimeID, "metadata", "Update", cancellationToken).ConfigureAwait(false))
        {
            var anime = _anidbAnimeRepository.GetByAnimeID(job.AnimeID);
            var update = _anidbAnimeUpdateRepository.GetByAnimeID(job.AnimeID);
            var animeRecentlyUpdated = AnimeRecentlyUpdated(anime, update);

            Exception? ex = null;
            ResponseGetAnime? response = null;
            if (job.PreferCacheOverRemote && job.UseCache)
            {
                try
                {
                    if (TryGetXmlFromCache(job.AnimeID, out var xml))
                        response = _httpParser.Parse(job.AnimeID, xml);
                }
                catch (Exception e)
                {
                    _logger.LogDebug(e, "Failed to parse the cached AnimeDoc_{AnimeID}.xml file", job.AnimeID);
                    ex = e;
                }
            }

            if (job.UseRemote && response is null)
            {
                try
                {
                    if (_httpConnectionHandler.IsBanned && !job.IgnoreHttpBans)
                    {
                        _logger.LogDebug("We're HTTP banned and requested a forced online update for anime with ID {AnimeID}", job.AnimeID);
                        throw new AniDBBannedException
                        {
                            BanType = UpdateType.HTTPBan,
                            BanExpires = _httpConnectionHandler.BanTime?.AddHours(_httpConnectionHandler.BanTimerResetLength),
                        };
                    }

                    if (animeRecentlyUpdated is null || job.IgnoreTimeCheck)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var request = _requestFactory.Create<RequestGetAnime>(r => (r.AnimeID, r.Force) = (job.AnimeID, job.IgnoreHttpBans));
                        var httpResponse = request.Send();
                        response = httpResponse.Response;

                        // If the response is null then we successfully got a response from the server
                        // but the ID does not belong to any anime.
                        if (response is null)
                        {
                            _logger.LogError("No such anime with ID: {AnimeID}", job.AnimeID);
                            return null;
                        }
                    }
                }
                catch (Exception e)
                {
                    if (e is AniDBBannedException)
                        _logger.LogTrace("We're HTTP banned and requested an online update for anime with ID {AnimeID}", job.AnimeID);
                    else
                        _logger.LogError(e, "Failed to get an anime with ID: {AnimeID}", job.AnimeID);

                    ex = e;
                }
            }

            if (!job.PreferCacheOverRemote && job.UseCache && response is null)
            {
                try
                {
                    if (TryGetXmlFromCache(job.AnimeID, out var xml))
                        response = _httpParser.Parse(job.AnimeID, xml);
                }
                catch (Exception e)
                {
                    _logger.LogDebug(e, "Failed to parse the cached AnimeDoc_{AnimeID}.xml file", job.AnimeID);
                    ex ??= e;
                }
            }

            // If we failed to get the data from either source then throw the
            // exception (if one exists) or return null.
            if (response is null)
            {
                if (job.DeferToRemoteIfUnsuccessful)
                {
                    _logger.LogDebug("Deferring to remote update for anime with ID {AnimeID}", job.AnimeID);
                    // Queue the command to get the data when we're no longer banned if there is no anime record.
                    await _scheduler.StartJob<GetRemoteAniDBAnimeJob>(
                        c =>
                        {
                            c.AnimeID = job.AnimeID;
                            c.DownloadRelations = job.DownloadRelations;
                            c.RelDepth = job.RelDepth;
                            c.CreateSeriesEntry = job.CreateSeriesEntry;
                            c.SkipSupplementaryUpdate = job.SkipSupplementaryUpdate;
                        },
                        // Only prioritize if we don't have an anime record.
                        prioritize: anime is null && animeRecentlyUpdated is null,
                        // Don't fire immediately if we recently updated the record.
                        startTime: animeRecentlyUpdated is not null ? DateTime.Now.AddHours(animeRecentlyUpdated.Value) : null
                    ).ConfigureAwait(false);
                }
                if (ex is null)
                {
                    // Anime data is fresh but a new file may have just been linked; refresh stats
                    // so missing-episode counts stay accurate without waiting for the next HTTP fetch.
                    if (anime is not null && _seriesRepository.GetByAnimeID(job.AnimeID) is not null)
                        await _scheduler.RunAfterCurrent<RefreshAnimeStatsJob>(j => j.AnimeID = job.AnimeID).ConfigureAwait(false);
                    return null;
                }
                throw ex;
            }

            // Create or update the anime record,
            anime ??= new AniDB_Anime();
            var isNew = anime.AniDB_AnimeID == 0;
            _animeCreator ??= _serviceProvider.GetRequiredService<AnimeCreator>();
            var (isUpdated, titlesUpdated, descriptionUpdated, shouldUpdateFiles, animeEpisodeChanges) = await _animeCreator.CreateAnime(response, anime, job.RelDepth).ConfigureAwait(false);

            {
                var relations = _anidbAnimeRelationRepository.GetByAnimeID(job.AnimeID);
                var unverified = relations.Where(r => !r.Verified).ToList();
                if (unverified.Count > 0)
                {
                    foreach (var rel in unverified)
                    {
                        if (rel.AbstractRelationType is not (RelationType.AlternativeSetting or RelationType.AlternativeVersion))
                        {
                            rel.Verified = true;
                            continue;
                        }

                        var reverseRelation = _anidbAnimeRelationRepository.GetByAnimeID(rel.RelatedAnimeID)
                            .SingleOrDefault(r => r.RelatedAnimeID == job.AnimeID);
                        if (reverseRelation is { Verified: true, AbstractRelationType: RelationType.AlternativeSetting or RelationType.AlternativeVersion })
                        {
                            rel.AbstractRelationType = reverseRelation.AbstractRelationType.Reverse();
                            rel.Verified = true;
                        }
                    }

                    var verifiedNow = unverified.Where(r => r.Verified).ToList();
                    if (verifiedNow.Count > 0)
                        _anidbAnimeRelationRepository.Save(verifiedNow);

                    if (relations.Any(r => !r.Verified))
                        await _scheduler.StartJob<VerifyAniDBRelationsJob>(c => c.AnimeID = job.AnimeID).ConfigureAwait(false);
                }
            }

            // then conditionally create the series record if it doesn't exist,
            var series = _seriesRepository.GetByAnimeID(job.AnimeID);
            var seriesIsNew = series == null;
            var seriesUpdated = false;
            var seriesEpisodeChanges = new Dictionary<AnimeEpisode, UpdateReason>();
            var settings = _settingsProvider.GetSettings();
            if (series == null && job.CreateSeriesEntry)
                series = await CreateAnimeSeriesAndGroup(anime, job, settings);

            // and then create or update the episode records if we have an
            // existing series record.
            if (series != null)
            {
                _seriesService ??= _serviceProvider.GetRequiredService<AnimeSeriesService>();
                (seriesUpdated, seriesEpisodeChanges) = await _seriesService.CreateAnimeEpisodes(series).ConfigureAwait(false);
                _seriesRepository.Save(series, true);
            }

            await _scheduler.RunAfterCurrent<RefreshAnimeStatsJob>(j => j.AnimeID = job.AnimeID).ConfigureAwait(false);

            // Request an image download
            await UpsertAndScheduleImageForEntity(anime, anime.Picname!, isDesired: true, forceDownload: false).ConfigureAwait(false);
            if (series is not null)
                await _scheduler.RunAfterCurrent<GetAniDBImagesJob>(c => c.AnimeID = job.AnimeID).ConfigureAwait(false);

            // Emit anidb anime updated event.
            if (isNew || isUpdated || animeEpisodeChanges.Count > 0)
                ShokoEventHandler.Instance.OnSeriesUpdated(anime, isNew ? UpdateReason.Added : UpdateReason.Updated, animeEpisodeChanges);

            // Reset the cached preferred title if anime titles were updated.
            if (titlesUpdated)
                anime.ResetPreferredTitle();

            // Reset the cached titles if anime titles were updated or if series is new.
            if ((titlesUpdated || seriesIsNew) && series is not null)
            {
                series.ResetPreferredTitle();
                series.ResetAnimeTitles();
            }

            // Reset the cached description if anime description was updated or if series is new.
            if ((descriptionUpdated || seriesIsNew) && series is not null)
            {
                series.ResetPreferredOverview();
            }

            // Emit shoko series updated event.
            if (series is not null && (seriesIsNew || seriesUpdated || seriesEpisodeChanges.Count > 0))
                ShokoEventHandler.Instance.OnSeriesUpdated(series, seriesIsNew ? UpdateReason.Added : UpdateReason.Updated, seriesEpisodeChanges);

            // Re-schedule the videos to move/rename as required if something changed.
            if (settings.Plugins.Renamer.RelocateOnImport && (
                isNew || shouldUpdateFiles || animeEpisodeChanges.Count > 0 || seriesIsNew || seriesUpdated || seriesEpisodeChanges.Count > 0
            ))
            {
                var videos = new List<VideoLocal>();
                if (isNew || seriesIsNew || shouldUpdateFiles || seriesUpdated)
                {
                    videos.AddRange(
                        _crossReferenceRepository.GetByAnimeID(job.AnimeID)
                            .WhereNotNull()
                            .Select(a => a.VideoLocal)
                            .WhereNotNull()
                            .DistinctBy(a => a.VideoLocalID)
                    );
                }
                else
                {
                    if (animeEpisodeChanges.Count > 0)
                        videos.AddRange(
                            animeEpisodeChanges.Keys
                                .SelectMany(a => _crossReferenceRepository.GetByEpisodeID(a.EpisodeID))
                                .WhereNotNull()
                                .Select(a => a.VideoLocal)
                                .WhereNotNull()
                                .DistinctBy(a => a.VideoLocalID)
                        );
                    if (seriesEpisodeChanges.Count > 0)
                        videos.AddRange(
                            seriesEpisodeChanges.Keys
                                .SelectMany(a => _crossReferenceRepository.GetByEpisodeID(a.AniDB_EpisodeID))
                                .WhereNotNull()
                                .Select(a => a.VideoLocal)
                                .WhereNotNull()
                                .DistinctBy(a => a.VideoLocalID)
                        );
                }

                foreach (var video in videos)
                    await _relocationService.ChainAutoRelocationForVideo(video, cancellationToken).ConfigureAwait(false);
            }

            if (!job.SkipSupplementaryUpdate)
                await _supplementaryMetadataService.ScheduleForAnime(anime.AnimeID, isNew: false).ConfigureAwait(false);

            await ProcessRelations(response, job, settings).ConfigureAwait(false);

            return anime;
        }
    }

    private int? AnimeRecentlyUpdated(AniDB_Anime? anime, AniDB_AnimeUpdate update)
    {
        if (anime != null && update != null)
        {
            var ts = DateTime.Now - update.UpdatedAt;
            var settings = _settingsProvider.GetSettings();
            if (ts.TotalHours < settings.AniDb.MinimumHoursToRedownloadAnimeInfo)
                return settings.AniDb.MinimumHoursToRedownloadAnimeInfo - (int)Math.Floor(ts.TotalHours);
        }

        return null;
    }

    private bool TryGetXmlFromCache(int animeID, [NotNullWhen(true)] out string? xml)
    {
        xml = _xmlUtils.LoadAnimeHTTPFromFile(animeID).GetAwaiter().GetResult();
        if (xml is not null)
            return true;

        if (_httpConnectionHandler.IsBanned)
            _logger.LogTrace("We're HTTP Banned and unable to find a cached AnimeDoc_{AnimeID}.xml file", animeID);
        else
            _logger.LogTrace("Unable to find a cached AnimeDoc_{AnimeID}.xml file", animeID);

        return false;
    }

    private async Task<AnimeSeries> CreateAnimeSeriesAndGroup(AniDB_Anime anime, AnidbJobDetails job, IServerSettings settings)
    {
        // Create a new AnimeSeries record
        var series = new AnimeSeries
        {
            AniDB_ID = anime.AnimeID,
            LatestLocalEpisodeNumber = 0,
            DateTimeUpdated = DateTime.Now,
            DateTimeCreated = DateTime.Now,
            UpdatedAt = DateTime.Now,
            SeriesNameOverride = string.Empty
        };

        var grp = _animeGroupCreator.GetOrCreateSingleGroupForAnime(anime);
        series.AnimeGroupID = grp.AnimeGroupID;
        // Populate before making a group to ensure IDs and stats are set for group filters.
        _seriesRepository.Save(series, false);
        if (!job.SkipSupplementaryUpdate)
            await _supplementaryMetadataService.ScheduleForAnime(anime.AnimeID, isNew: true).ConfigureAwait(false);

        return series;
    }

    private async Task ProcessRelations(ResponseGetAnime response, AnidbJobDetails job, IServerSettings settings)
    {
        if (!job.DownloadRelations) return;
        if (settings.AniDb.MaxRelationDepth <= 0) return;
        if (job.RelDepth >= settings.AniDb.MaxRelationDepth) return;
        if (!settings.AutoGroupSeries && !settings.AniDb.DownloadRelatedAnime) return;

        // Queue or process the related series.
        foreach (var relation in response.Relations)
        {
            // Skip queuing/processing the command if the anime record were
            // recently updated.
            var anime = _anidbAnimeRepository.GetByAnimeID(relation.RelatedAnimeID);
            if (anime != null)
            {
                // Check when the anime was last updated online if we are
                // forcing a refresh, and we're not banned, otherwise check when
                // the local anime record was last updated (be it from a fresh
                // online xml file or from a cached xml file).
                var update = _anidbAnimeUpdateRepository.GetByAnimeID(relation.RelatedAnimeID);
#pragma warning disable CS0618
                var updatedAt = job.UseRemote && (job.IgnoreHttpBans || !_httpConnectionHandler.IsBanned) && update != null ? update.UpdatedAt : anime.DateTimeUpdated;
#pragma warning restore CS0618
                var ts = DateTime.Now - updatedAt;
                if (ts.TotalHours < settings.AniDb.MinimumHoursToRedownloadAnimeInfo) continue;
            }

            _logger.LogInformation("Queuing/processing anime relation: {AnimeID} -> {RelatedAnimeID}", relation.AnimeID, relation.RelatedAnimeID);

            // Append the command to the queue.
            if (!job.UseCache && job.UseRemote)
                await _scheduler.StartJob<GetRemoteAniDBAnimeJob>(c =>
                {
                    c.AnimeID = relation.RelatedAnimeID;
                    c.DownloadRelations = true;
                    c.RelDepth = job.RelDepth + 1;
                    c.IgnoreTimeCheck = job.IgnoreTimeCheck;
                    c.IgnoreHttpBans = job.IgnoreHttpBans;
                    c.CreateSeriesEntry = job.CreateSeriesEntry && settings.AniDb.AutomaticallyImportSeries;
                    c.SkipSupplementaryUpdate = job.SkipSupplementaryUpdate;
                }, prioritize: true).ConfigureAwait(false);
            else
                await _scheduler.StartJob<GetAniDBAnimeJob>(c =>
                {
                    c.AnimeID = relation.RelatedAnimeID;
                    c.DownloadRelations = true;
                    c.RelDepth = job.RelDepth + 1;
                    c.UseCache = job.UseCache;
                    c.UseRemote = job.UseRemote;
                    c.IgnoreTimeCheck = job.IgnoreTimeCheck;
                    c.IgnoreHttpBans = job.IgnoreHttpBans;
                    c.CreateSeriesEntry = job.CreateSeriesEntry && settings.AniDb.AutomaticallyImportSeries;
                    c.SkipSupplementaryUpdate = job.SkipSupplementaryUpdate;
                }, prioritize: true).ConfigureAwait(false);
        }
    }
    #endregion

    #endregion

    #region AniDB Tags

    public IEnumerable<IAnidbTag> GetAllTags(bool topLevelOnly = false)
    {
        var tags = _anidbTagRepository.GetAll();
        if (topLevelOnly)
            return tags.Where(t => t.ParentTagID is 0);
        return tags;
    }

    #endregion

    #region Images

    public async Task ScheduleImagesForAnimeByID(int anidbAnimeID, bool onlyPosters = false, bool forceDownload = false, bool prioritize = true)
    {
        await _scheduler.StartJob<GetAniDBImagesJob>(
            c => (c.AnimeID, c.OnlyPosters, c.ForceDownload) = (anidbAnimeID, onlyPosters, forceDownload),
            prioritize: prioritize
        ).ConfigureAwait(false);
    }

    public async Task ProcessImagesForAnimeByID(int anidbAnimeID, bool onlyPosters = false, bool forceDownload = false)
    {
        if (_anidbAnimeRepository.GetByAnimeID(anidbAnimeID) is null)
            return;

        using (await _entityLock.GetLockForEntityAsync(DataEntityType.Anime, anidbAnimeID, "images", "Update").ConfigureAwait(false))
        {
            if (_anidbAnimeRepository.GetByAnimeID(anidbAnimeID) is not { } anime)
                return;

            var settings = _settingsProvider.GetSettings();

            await UpsertAndScheduleImageForEntity(anime, anime.Picname, isDesired: true, forceDownload).ConfigureAwait(false);
            if (onlyPosters)
                return;

            var characters = _anidbAnimeCharacterRepository.GetByAnimeID(anidbAnimeID)
                .Select(xref => _anidbCharacterRepository.GetByCharacterID(xref.CharacterID))
                .WhereNotNull()
                .Where(character => !string.IsNullOrEmpty(character.ImagePath))
                .DistinctBy(character => character.CharacterID)
                .ToList();
            foreach (var character in characters)
                await UpsertAndScheduleImageForEntity(character, character.ImagePath, settings.AniDb.DownloadCharacters, forceDownload).ConfigureAwait(false);

            var voiceActors = _anidbAnimeCharacterCreatorRepository.GetByAnimeID(anidbAnimeID)
                .Select(xref => _anidbCreatorRepository.GetByCreatorID(xref.CreatorID))
                .WhereNotNull()
                .Where(creator => !string.IsNullOrEmpty(creator.ImagePath));
            var staffMembers = _anidbAnimeStaffRepository.GetByAnimeID(anidbAnimeID)
                .Select(xref => _anidbCreatorRepository.GetByCreatorID(xref.CreatorID))
                .WhereNotNull()
                .Where(creator => !string.IsNullOrEmpty(creator.ImagePath));
            var creators = voiceActors.Concat(staffMembers)
                .DistinctBy(creator => creator.CreatorID)
                .ToList();
            foreach (var creator in creators)
            {
                await UpsertAndScheduleImageForEntity(creator, creator.ImagePath, settings.AniDb.DownloadCreators, forceDownload).ConfigureAwait(false);
                if (creator.Type is CreatorType.Company)
                    await UpsertAndScheduleImageForEntity(new AniDB_Studio(creator), creator.ImagePath, settings.AniDb.DownloadCreators, forceDownload).ConfigureAwait(false);
            }
        }
    }

    public async Task ProcessImagesForCreatorByID(int creatorID, bool forceDownload = false)
    {
        if (_anidbCreatorRepository.GetByCreatorID(creatorID) is not { } creator || string.IsNullOrEmpty(creator.ImagePath))
            return;

        using (await _entityLock.GetLockForEntityAsync(DataEntityType.Creator, creatorID, "images", "Update").ConfigureAwait(false))
        {
            var desired = _settingsProvider.GetSettings().AniDb.DownloadCreators;
            await UpsertAndScheduleImageForEntity(creator, creator.ImagePath, desired, forceDownload).ConfigureAwait(false);
            if (creator.Type is CreatorType.Company)
                await UpsertAndScheduleImageForEntity(new AniDB_Studio(creator), creator.ImagePath, desired, forceDownload).ConfigureAwait(false);
        }
    }

    private async Task UpsertAndScheduleImageForEntity(IWithImages entity, string? resourceID, bool isDesired, bool forceDownload)
    {
        if (string.IsNullOrWhiteSpace(resourceID))
            return;

        var image = _imageManager.GetImageBySourceAndRemoteResourceID(DataSource.AniDB, resourceID)
            ?? _imageManager.AddImage(new ImageData
            {
                Source = DataSource.AniDB,
                ResourceID = resourceID,
            });

        var imageXref = _imageManager.GetImageCrossReferencesForEntity(entity, new() { ImageSource = DataSource.AniDB, ImageType = ImageEntityType.Primary, XrefSource = DataSource.AniDB })
            .FirstOrDefault(xref => xref.ImageID == image.ID)
            ?? _imageManager.AddImageCrossReference(entity, image, new ImageCrossReferenceData
            {
                Source = DataSource.AniDB,
                ImageType = ImageEntityType.Primary,
                IsDesired = isDesired,
            });

        _imageManager.UpdateImageCrossReference(imageXref, new ImageCrossReferenceUpdateData
        {
            IsDesired = isDesired,
        });

        if (isDesired)
            await _imageManager.ScheduleDownloadOfImage(image, forceDownload).ConfigureAwait(false);
    }

    #endregion

    #region Purge

    public async Task PurgeAllUnusedAnime()
    {
        var allAnimeIds = _anidbAnimeRepository.GetAll()
            .Select(anime => anime.AnimeID)
            .Concat(_shokoImageXrefRepository.GetByEntity(DataSource.AniDB, DataEntityType.Anime).Select(xref => int.TryParse(xref.EntityID, out var id) ? id : 0))
            .Concat(_anidbEpisodeRepository.GetAll().Select(episode => episode.AnimeID))
            .Concat(_anidbAnimeCharacterRepository.GetAll().Select(xref => xref.AnimeID))
            .Concat(_anidbAnimeCharacterCreatorRepository.GetAll().Select(xref => xref.AnimeID))
            .Concat(_anidbAnimeStaffRepository.GetAll().Select(xref => xref.AnimeID))
            .Concat(_anidbAnimeTagRepository.GetAll().Select(xref => xref.AnimeID))
            .Concat(_anidbAnimeTitleRepository.GetAll().Select(title => title.AnimeID))
            .Concat(_anidbAnimeUpdateRepository.GetAll().Select(update => update.AnimeID))
            .Concat(_storedReleaseInfoRepository.GetAll().SelectMany(release => release.CrossReferences.Select(xref => xref.AnidbAnimeID).WhereNotNull()))
            .Where(id => id > 0)
            .ToHashSet();
        var toKeep = _seriesRepository.GetAll().Select(series => series.AniDB_ID).Where(id => id > 0).ToHashSet();
        var toBePurged = allAnimeIds.Except(toKeep).ToHashSet();

        _logger.LogInformation("Scheduling {Count} out of {AllCount} AniDB anime entries to be purged.", toBePurged.Count, allAnimeIds.Count);
        foreach (var animeID in toBePurged)
            await _scheduler.StartJob<PurgeAniDBAnimeJob>(c => c.AnimeID = animeID).ConfigureAwait(false);
    }

    public async Task SchedulePurgeOfAnimeByID(int anidbAnimeID, bool removeFromMylist = true, bool prioritize = false)
    {
        await _scheduler.StartJob<PurgeAniDBAnimeJob>(c =>
        {
            c.AnimeID = anidbAnimeID;
            c.RemoveFromMylist = removeFromMylist;
        }, prioritize: prioritize).ConfigureAwait(false);
    }

    public async Task PurgeAnimeByID(int anidbAnimeID, bool removeFromMylist = true)
    {
        using (await _entityLock.GetLockForEntityAsync(DataEntityType.Anime, anidbAnimeID, "metadata", "Purge").ConfigureAwait(false))
        {
            // Ensure shoko entities are removed first when they still exist.
            if (_seriesRepository.GetByAnimeID(anidbAnimeID) is { } series)
            {
                _seriesService ??= _serviceProvider.GetRequiredService<AnimeSeriesService>();
                await _seriesService.DeleteSeriesInternal(series, deleteFiles: false, updateGroups: true, removeFromMylist).ConfigureAwait(false);
            }

            var anime = _anidbAnimeRepository.GetByAnimeID(anidbAnimeID);
            var characterXrefs = _anidbAnimeCharacterRepository.GetByAnimeID(anidbAnimeID);
            var actorXrefs = _anidbAnimeCharacterCreatorRepository.GetByAnimeID(anidbAnimeID);
            var staffXrefs = _anidbAnimeStaffRepository.GetByAnimeID(anidbAnimeID);
            var characters = characterXrefs
                .Select(x => x.Character)
                .WhereNotNull()
                .Where(x => !_anidbAnimeCharacterRepository.GetByCharacterID(x.CharacterID).ExceptBy(characterXrefs.Select(y => y.AniDB_Anime_CharacterID), y => y.AniDB_Anime_CharacterID).Any())
                .ToList();
            var creators = actorXrefs.Select(x => x.Creator)
                .Concat(staffXrefs.Select(x => x.Creator))
                .WhereNotNull()
                .Where(x =>
                    !x.Staff.ExceptBy(staffXrefs.Select(y => y.AniDB_Anime_StaffID), y => y.AniDB_Anime_StaffID).Any() &&
                    !x.Characters.ExceptBy(actorXrefs.Select(y => y.AniDB_Anime_Character_CreatorID), y => y.AniDB_Anime_Character_CreatorID).Any()
                )
                .ToList();
            var tagXrefs = _anidbAnimeTagRepository.GetByAnimeID(anidbAnimeID);
            var titles = _anidbAnimeTitleRepository.GetByAnimeID(anidbAnimeID);
            var anidbEpisodes = _anidbEpisodeRepository.GetByAnimeID(anidbAnimeID);
            var episodeTitles = anidbEpisodes.SelectMany(a => _anidbEpisodeTitleRepository.GetByEpisodeID(a.EpisodeID)).ToList();
            var update = _anidbAnimeUpdateRepository.GetByAnimeID(anidbAnimeID);

            _anidbAnimeCharacterRepository.Delete(characterXrefs);
            _anidbCharacterRepository.Delete(characters);
            _anidbAnimeCharacterCreatorRepository.Delete(actorXrefs);
            _anidbAnimeStaffRepository.Delete(staffXrefs);
            _anidbCreatorRepository.Delete(creators);
            _anidbAnimeTagRepository.Delete(tagXrefs);
            _anidbAnimeTitleRepository.Delete(titles);
            _anidbEpisodeTitleRepository.Delete(episodeTitles);
            _anidbEpisodeRepository.Delete(anidbEpisodes);
            _anidbAnimeUpdateRepository.Delete(update);

            // Explicitly remove image cross references through the image manager.
            PurgeImageXrefsForEntity(DataEntityType.Anime, anidbAnimeID);
            PurgeImageXrefsForEntity(DataEntityType.Season, AniDB_Season.GetID(anidbAnimeID, EpisodeType.Episode, 1));
            PurgeImageXrefsForEntity(DataEntityType.Season, AniDB_Season.GetID(anidbAnimeID, EpisodeType.Special, 0));
            foreach (var episode in anidbEpisodes)
                PurgeImageXrefsForEntity(DataEntityType.Episode, episode.EpisodeID);
            foreach (var character in characters)
                PurgeImageXrefsForEntity(DataEntityType.Character, character.CharacterID);
            foreach (var creator in creators)
            {
                PurgeImageXrefsForEntity(DataEntityType.Creator, creator.CreatorID);
                PurgeImageXrefsForEntity(DataEntityType.Studio, creator.CreatorID);
            }

            // remove all releases linked to this anime.
            var releases = _storedReleaseInfoRepository.GetByAnidbAnimeID(anidbAnimeID);
            var releaseService = _serviceProvider.GetRequiredService<IVideoReleaseService>();
            foreach (var release in releases)
                await releaseService.RemoveRelease(release, removeFromMylist).ConfigureAwait(false);

            if (anime is not null)
            {
                _logger.LogTrace("Removing AniDB anime {AnimeTitle} (Anime={AnimeID})", anime.MainTitle, anime.AnimeID);
                _anidbAnimeRepository.Delete(anime);

                ShokoEventHandler.Instance.OnSeriesUpdated(anime, UpdateReason.Removed);
            }
        }
    }

    private void PurgeImageXrefsForEntity(DataEntityType entityType, int entityID)
        => PurgeImageXrefsForEntity(entityType, entityID.ToString());

    private void PurgeImageXrefsForEntity(DataEntityType entityType, string entityID)
    {
        var xrefs = _shokoImageXrefRepository.GetByEntity(DataSource.AniDB, entityType, entityID.ToString());
        foreach (var xref in xrefs)
            _imageManager.RemoveImageCrossReference(xref);
    }

    #endregion

    #region AVDump

    /// <inheritdoc/>
    public event EventHandler<AnidbAvdumpEventArgs>? AvdumpEvent;

    /// <inheritdoc/>
    public bool IsAvdumpInstalled => AVDumpHelper.IsAVDumpInstalled;

    /// <inheritdoc/>
    public string? InstalledAvdumpVersion => AVDumpHelper.InstalledAVDumpVersion;

    /// <inheritdoc/>
    public string? AvailableAvdumpVersion => AVDumpHelper.AVDumpVersion;

    /// <inheritdoc/>
    public bool UpdateAvdump(bool force = false)
    {
        if (!force)
        {
            var expectedVersion = AVDumpHelper.AVDumpVersion;
            var installedVersion = AVDumpHelper.InstalledAVDumpVersion;
            if (string.Equals(expectedVersion, installedVersion))
                return false;
        }

        return AVDumpHelper.UpdateAVDump();
    }

    private void OnAVDumpEvent(object? sender, AnidbAvdumpEventArgs eventArgs)
    {
        AvdumpEvent?.Invoke(this, eventArgs);
    }

    /// <inheritdoc/>
    public async Task AvdumpVideos(params IVideo[] videos)
    {
        var videoSet = new HashSet<int>();
        var videoDictionary = new Dictionary<int, string>();
        foreach (var video in videos)
        {
            if (!videoSet.Add(video.ID))
                continue;

            if (video.Files.FirstOrDefault(x => x.IsAvailable) is not { } location)
                continue;

            videoDictionary.Add(video.ID, location.Path);
        }

        await Task.Run(() => AVDumpHelper.DumpFiles(videoDictionary)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ScheduleAvdumpVideos(params IVideo[] videos)
    {
        var videoSet = new HashSet<int>();
        var videoDictionary = new Dictionary<int, string>();
        foreach (var video in videos)
        {
            if (!videoSet.Add(video.ID))
                continue;

            if (video.Files.FirstOrDefault(x => x.IsAvailable) is not { } location)
                continue;

            videoDictionary.Add(video.ID, location.Path);
        }
        await _scheduler.StartJob<AVDumpFilesJob>(a => a.Videos = videoDictionary, prioritize: true).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task AvdumpVideoFiles(params IVideoFile[] videoFiles)
    {
        var videoSet = new HashSet<int>();
        var videoDictionary = new Dictionary<int, string>();
        foreach (var videoFile in videoFiles)
        {
            if (!videoFile.IsAvailable)
                continue;

            if (!videoSet.Add(videoFile.VideoID))
                continue;

            videoDictionary.Add(videoFile.ID, videoFile.Path);
        }

        await Task.Run(() => AVDumpHelper.DumpFiles(videoDictionary)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ScheduleAvdumpVideoFiles(params IVideoFile[] videoFiles)
    {
        var videoSet = new HashSet<int>();
        var videoDictionary = new Dictionary<int, string>();
        foreach (var videoFile in videoFiles)
        {
            if (!videoFile.IsAvailable)
                continue;

            if (!videoSet.Add(videoFile.VideoID))
                continue;

            videoDictionary.Add(videoFile.ID, videoFile.Path);
        }
        await _scheduler.StartJob<AVDumpFilesJob>(a => a.Videos = videoDictionary, prioritize: true).ConfigureAwait(false);
    }

    #endregion

    #region Helper Classes

    private class AbstractAnidbAnimeSearchResult(SeriesSearch.SearchResult<ResponseAniDBTitles.Anime> searchResult, AniDB_AnimeRepository animeAnimeRepository, AnimeSeriesRepository seriesRepository) : IAnidbAnimeSearchResult
    {
        private readonly AniDB_AnimeRepository _anidbAnimeRepository = animeAnimeRepository;

        private readonly AnimeSeriesRepository _seriesRepository = seriesRepository;

        private IReadOnlyList<ITitle>? _titles = null;

        private IAnidbAnime? _anidbAnime = null;

        private IShokoSeries? _shokoSeries = null;

        /// <inheritdoc/>
        public int ID { get; init; } = searchResult.Result.AnimeID;

        /// <inheritdoc/>
        public DataSource Source => DataSource.AniDB;

        public string Title { get; init; } = searchResult.Result.Title;

        /// <inheritdoc/>
        public ITitle DefaultTitle { get; init; } = searchResult.Result.DefaultTitle;

        /// <inheritdoc/>
        public ITitle? PreferredTitle { get; init; } = searchResult.Result.PreferredTitle;

        /// <inheritdoc/>
        public IReadOnlyList<ITitle> Titles => _titles ??= searchResult.Result.Titles;

        /// <inheritdoc/>
        public string MatchedTitle { get; init; } = searchResult.Match;

        /// <inheritdoc/>
        public bool ExactMatch { get; init; } = searchResult.ExactMatch;

        /// <inheritdoc/>
        public int Index { get; init; } = searchResult.Index;

        /// <inheritdoc/>
        public double Distance { get; init; } = searchResult.Distance;

        /// <inheritdoc/>
        public int LengthDifference { get; init; } = searchResult.LengthDifference;

        /// <inheritdoc/>
        public IAnidbAnime? AnidbAnime => ID > 0 ? _anidbAnime ??= _anidbAnimeRepository.GetByAnimeID(ID) : null;

        /// <inheritdoc/>
        public IShokoSeries? ShokoSeries => ID > 0 ? _shokoSeries ??= _seriesRepository.GetByAnimeID(ID) : null;
    }

    private class AnidbJobDetails
    {
        /// <summary>
        /// The ID of the AniDB anime to update.
        /// </summary>
        public int AnimeID { get; set; }

        /// <summary>
        /// Use the remote AniDB HTTP API.
        /// </summary>
        public bool UseRemote { get; set; } = true;

        /// <summary>
        /// Use the local AniDB HTTP cache.
        /// </summary>
        public bool UseCache { get; set; } = true;

        /// <summary>
        /// Prefer the local AniDB HTTP cache over the remote AniDB HTTP API.
        /// </summary>
        public bool PreferCacheOverRemote { get; set; }

        /// <summary>
        /// Defer to a later remote update if the current update fails.
        /// </summary>
        public bool DeferToRemoteIfUnsuccessful { get; set; } = true;

        /// <summary>
        /// Ignore the time check and forces a refresh even if the anime was
        /// recently updated.
        /// </summary>
        public bool IgnoreTimeCheck { get; set; }

        /// <summary>
        /// Ignore any active HTTP bans and forcefully asks the server for the data.
        /// </summary>
        public bool IgnoreHttpBans { get; set; }

        /// <summary>
        /// Download related anime until the maximum depth is reached.
        /// </summary>
        public bool DownloadRelations { get; set; }

        /// <summary>
        /// Create a Shoko series entry if one does not exist.
        /// </summary>
        public bool CreateSeriesEntry { get; set; }

        /// <summary>
        /// Skip updating related TMDB entities after update.
        /// </summary>
        public bool SkipSupplementaryUpdate { get; set; }

        /// <summary>
        /// Current depth of recursion.
        /// </summary>
        public int RelDepth { get; set; }
    }

    #endregion
}
