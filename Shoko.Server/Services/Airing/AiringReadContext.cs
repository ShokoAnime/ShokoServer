using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;
using Shoko.Server.Models.Airing;
using Shoko.Server.Models.AniDB.Embedded;
using Shoko.Server.Models.Shoko.Embedded;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities.Airing;

#nullable enable
namespace Shoko.Server.Services.Airing;

/// <summary>
/// The scratch pad one read runs in. Every entity, channel, provider, schedule
/// view, link set and estimate profile the read touches is resolved at most
/// once here, so a week of airings across twenty channels costs one resolution
/// per distinct thing rather than one per airing.
/// </summary>
/// <remarks>
/// A context lives for the length of a single service call. Views hold on to
/// it and resolve through it lazily, which is what keeps a caller that only
/// wants times from paying for entity lookups at all.
/// </remarks>
internal sealed class AiringReadContext
{
    private readonly AiringScheduleService _service;

    private readonly Dictionary<int, AiringScheduleView> _scheduleViews = [];

    private readonly Dictionary<(MetadataSource Source, string ID), ISeries?> _series = [];

    private readonly Dictionary<(MetadataSource Source, string ID), ISeason?> _seasons = [];

    private readonly Dictionary<(MetadataSource Source, string ID), IReadOnlySet<(MetadataSource Source, string ID)>> _seasonEpisodeKeys = [];

    private readonly Dictionary<(MetadataSource Source, string ID), IEpisode?> _episodes = [];

    private readonly Dictionary<Guid, AiringScheduleProviderInfo?> _providers = [];

    private readonly Dictionary<Guid, IAiringChannel?> _channels = [];

    private readonly Dictionary<(MetadataSource Source, string ID), IReadOnlyList<(MetadataSource Source, string ID)>> _linkedEpisodeKeys = [];

    private readonly Dictionary<int, IReadOnlyDictionary<int, IEpisode?>> _scheduleEpisodesByNumber = [];

    private readonly Dictionary<(int ScheduleID, int EpisodeNumber), (int AnimeID, int EpisodeNumber)?> _anidbPositions = [];

    private readonly Dictionary<int, IReadOnlyDictionary<int, IAnidbEpisode>> _anidbEpisodesByNumber = [];

    private readonly Dictionary<int, IAnidbAnime?> _anidbAnime = [];

    private readonly Dictionary<int, IShokoSeries?> _shokoSeriesByAnimeID = [];

    private readonly Dictionary<(TargetMemoKey Target, bool EpisodeLinked, bool PositionLinked), IReadOnlyList<(AiringSchedule Schedule, EpisodeAiring Entry)>> _storedAirings = [];

    private readonly Dictionary<TargetMemoKey, DateTime?> _firstOriginalAirings = [];

    private readonly Dictionary<(MetadataSource Source, string ID), DateTime?> _anidbAirDates = [];

    private readonly Dictionary<MetadataGuid, AiringSeriesState> _seriesStates = [];

    private readonly Dictionary<TargetMemoKey, IReadOnlyList<(AiringSchedule Schedule, DateTime AiredAt)>> _firstNormalAirings = [];

    private readonly Dictionary<int, bool> _detectedReruns = [];

    private readonly Dictionary<(int One, int Other), bool> _sharedTracks = [];

    private readonly Dictionary<int, IReadOnlyList<IMetadataSeriesCrossReference>> _seriesLinks = [];

    private readonly Dictionary<int, IReadOnlyList<IMetadataEpisodeCrossReference>> _episodeLinks = [];

    private readonly Dictionary<(MetadataSource Source, string ID), IReadOnlyList<int>> _linkedAnimeIDs = [];

    private readonly Dictionary<(int AnimeID, int ScheduleID), AiringEpisodeOffset?> _episodeOffsets = [];

    private readonly Dictionary<int, TimeSpan?> _usualEpisodeLengths = [];

    private readonly Dictionary<Guid, List<DateTime>> _channelSlots = [];

    private AiringScheduleServiceSettings? _settings;

    private IReadOnlySet<Guid>? _hiddenChannels;

    private (IFilter Filter, IReadOnlySet<int> AnimeIDs)? _filteredAnimeIDs;

    /// <summary>
    /// Whether schedules whose provider is gone or disabled, and tracks of a
    /// disabled kind, are part of this read.
    /// </summary>
    public bool IncludeDisabled { get; }

    /// <summary>
    /// Whether the schedules on hidden channels are part of this read even
    /// when it names no channels, as the notification horizon needs.
    /// </summary>
    public bool IncludeHiddenChannels { get; }

    /// <summary>
    /// What the read counts as now, in UTC: the time it is as of, or the
    /// service's clock taken once when the read starts, so every part of it
    /// agrees.
    /// </summary>
    public DateTime Now { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AiringReadContext"/> class.
    /// </summary>
    /// <param name="service">The service the read belongs to.</param>
    /// <param name="includeDisabled">Whether disabled providers and kinds are part of the read.</param>
    /// <param name="includeHiddenChannels">Whether hidden channels are part of the read when it names no channels.</param>
    /// <param name="at">The time the read is as of, or <c>null</c> for now; a time without a kind is UTC.</param>
    /// <exception cref="ArgumentNullException"><paramref name="service"/> is <c>null</c>.</exception>
    public AiringReadContext(AiringScheduleService service, bool includeDisabled = false, bool includeHiddenChannels = false, DateTime? at = null)
    {
        ArgumentNullException.ThrowIfNull(service);

        _service = service;
        IncludeDisabled = includeDisabled;
        IncludeHiddenChannels = includeHiddenChannels;
        Now = at is { } value ? ToUtc(value) : service.UtcNow;
    }

    /// <summary>
    /// A time as UTC. A time without a kind is UTC already, and a local one is
    /// converted.
    /// </summary>
    /// <param name="value">The time.</param>
    /// <returns>The time, in UTC.</returns>
    internal static DateTime ToUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value.ToUniversalTime(),
        };

    #region Settings

    /// <summary>
    /// The service's settings as they stood when the read started, loaded once
    /// rather than per airing: a load goes through the configuration provider,
    /// which is far too much to pay per ordered element.
    /// </summary>
    public AiringScheduleServiceSettings Settings => _settings ??= _service.LoadSettings();

    /// <summary>
    /// The channels the server hides from reads, as a set built once per read.
    /// </summary>
    public IReadOnlySet<Guid> HiddenChannels => _hiddenChannels ??= _service.HiddenChannelIDs;

    #endregion

    #region Views

    /// <summary>
    /// The view over a schedule row, shared by every airing of that schedule in
    /// this read.
    /// </summary>
    /// <param name="row">The schedule row.</param>
    /// <returns>The schedule view.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is <c>null</c>.</exception>
    public AiringScheduleView GetSchedule(AiringSchedule row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (_scheduleViews.TryGetValue(row.AiringScheduleID, out var view))
            return view;

        return _scheduleViews[row.AiringScheduleID] = new AiringScheduleView(this, row);
    }

    /// <summary>
    /// The view over the schedule a row belongs to, or <c>null</c>
    /// when the schedule is gone.
    /// </summary>
    /// <param name="scheduleID">The local ID of the schedule.</param>
    /// <returns>The schedule view, or <c>null</c>.</returns>
    public AiringScheduleView? GetSchedule(int scheduleID)
        => RepoFactory.AiringSchedule.GetByID(scheduleID) is { } row ? GetSchedule(row) : null;

    /// <summary>
    /// The estimate profile of a schedule, learned once per schedule and reused
    /// for every episode of it.
    /// </summary>
    /// <param name="row">The schedule row.</param>
    /// <returns>The schedule's profile.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is <c>null</c>.</exception>
    public AiringScheduleProfile GetProfile(AiringSchedule row)
        => _service.GetProfile(row, this);

    #endregion

    #region Providers & Channels

    /// <summary>
    /// The registered provider behind an ID, or <c>null</c> when the
    /// plugin that supplied it is gone.
    /// </summary>
    /// <param name="providerID">The ID of the provider.</param>
    /// <returns>The provider info, or <c>null</c>.</returns>
    public AiringScheduleProviderInfo? GetProvider(Guid providerID)
    {
        // The service hands back a fresh copy every time, so a read that asks
        // per airing pays for one info object and one kind set per ask.
        if (_providers.TryGetValue(providerID, out var provider))
            return provider;

        return _providers[providerID] = _service.GetProviderInfo(providerID);
    }

    /// <summary>
    /// Whether a provider counts for this read: it has to be registered and
    /// enabled, unless the read asked for disabled ones too.
    /// </summary>
    /// <param name="providerID">The ID of the provider.</param>
    /// <returns><c>true</c> when the provider's data is part of this read.</returns>
    public bool IsProviderVisible(Guid providerID)
        => IncludeDisabled || GetProvider(providerID) is { Enabled: true };

    /// <summary>
    /// The kinds a provider currently has enabled, or every kind when the read
    /// includes disabled data or the provider is gone.
    /// </summary>
    /// <param name="providerID">The ID of the provider.</param>
    /// <returns>The kinds whose tracks are visible.</returns>
    public IReadOnlySet<AiringKind> GetVisibleKinds(Guid providerID)
        => IncludeDisabled || GetProvider(providerID) is not { } info ? AiringScheduleService.AllKinds : info.EnabledKinds;

    /// <summary>
    /// The channel behind an ID, resolved once per read.
    /// </summary>
    /// <param name="channelID">The ID of the channel.</param>
    /// <returns>The channel, or <c>null</c> when it is unknown.</returns>
    public IAiringChannel? GetChannel(Guid channelID)
    {
        if (_channels.TryGetValue(channelID, out var channel))
            return channel;

        return _channels[channelID] = RepoFactory.AiringChannel.GetByChannelID(channelID) is { } row
            ? new AiringChannelView(row)
            : null;
    }

    #endregion

    #region Entities

    /// <summary>
    /// The series behind a stored key, resolved once per read.
    /// </summary>
    /// <param name="source">The source of the series.</param>
    /// <param name="id">The ID of the series within its source.</param>
    /// <returns>The series, or <c>null</c> when it can't be resolved.</returns>
    public ISeries? GetSeries(MetadataSource source, string id)
    {
        if (_series.TryGetValue((source, id), out var series))
            return series;

        return _series[(source, id)] = ResolveSeries(source, id);
    }

    /// <summary>
    /// The season behind a stored key, resolved once per read.
    /// </summary>
    /// <param name="source">The source of the season.</param>
    /// <param name="id">The ID of the season within its source.</param>
    /// <returns>The season, or <c>null</c> when it can't be resolved.</returns>
    public ISeason? GetSeason(MetadataSource source, string id)
    {
        if (_seasons.TryGetValue((source, id), out var season))
            return season;

        return _seasons[(source, id)] = ResolveSeason(source, id);
    }

    /// <summary>
    /// The stored keys of a season's episodes, materialized once per read.
    /// <see cref="ISeason.Episodes"/> is rebuilt from scratch on every access,
    /// so a membership test that goes through it per episode costs the season's
    /// whole episode list per episode.
    /// </summary>
    /// <param name="source">The source of the season.</param>
    /// <param name="id">The ID of the season within its source.</param>
    /// <returns>The keys, or an empty set when the season can't be resolved.</returns>
    public IReadOnlySet<(MetadataSource Source, string ID)> GetSeasonEpisodeKeys(MetadataSource source, string id)
    {
        if (_seasonEpisodeKeys.TryGetValue((source, id), out var keys))
            return keys;

        return _seasonEpisodeKeys[(source, id)] = GetSeason(source, id) is { } season
            ? season.Episodes.Select(episode => AiringScheduleService.GetEntityKey(episode)).ToHashSet()
            : new HashSet<(MetadataSource Source, string ID)>();
    }

    /// <summary>
    /// The episode behind a stored key, resolved once per read.
    /// </summary>
    /// <param name="source">The source of the episode.</param>
    /// <param name="id">The ID of the episode within its source.</param>
    /// <returns>The episode, or <c>null</c> when it can't be resolved.</returns>
    public IEpisode? GetEpisode(MetadataSource source, string id)
    {
        if (_episodes.TryGetValue((source, id), out var episode))
            return episode;

        return _episodes[(source, id)] = ResolveEpisode(source, id);
    }

    /// <summary>
    /// Remember an entity the caller already handed us, so a read for an
    /// episode never looks that episode up again.
    /// </summary>
    /// <param name="episode">The episode to remember.</param>
    /// <exception cref="ArgumentNullException"><paramref name="episode"/> is <c>null</c>.</exception>
    public void Remember(IEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);

        _episodes[(episode.Source, episode.ID.ID)] = episode;
    }

    private ISeries? ResolveSeries(MetadataSource source, string id)
        => source switch
        {
            _ when source == MetadataSource.Shoko => int.TryParse(id, out var shokoSeriesID) ? RepoFactory.AnimeSeries.GetByID(shokoSeriesID) : null,
            _ when source == MetadataSource.AniDB => int.TryParse(id, out var anidbAnimeID) ? RepoFactory.AniDB_Anime.GetByAnimeID(anidbAnimeID) : null,
            _ => ResolveThroughResolvers(source, MetadataEntityType.Series, id) as ISeries,
        };

    private ISeason? ResolveSeason(MetadataSource source, string id)
        => source switch
        {
            _ when source == MetadataSource.Shoko => ParseEmbeddedSeasonID(id) is not { } shoko || RepoFactory.AnimeSeries.GetByID(shoko.ID) is not { } shokoSeries
                ? null : new AnimeSeason(shokoSeries, shoko.Type, shoko.Number),
            _ when source == MetadataSource.AniDB => ParseEmbeddedSeasonID(id) is not { } anidb || RepoFactory.AniDB_Anime.GetByAnimeID(anidb.ID) is not { } anidbAnime
                ? null : new AniDB_Season(anidbAnime, anidb.Type, anidb.Number),
            _ => ResolveThroughResolvers(source, MetadataEntityType.Season, id) as ISeason,
        };

    private IEpisode? ResolveEpisode(MetadataSource source, string id)
        => source switch
        {
            _ when source == MetadataSource.Shoko => int.TryParse(id, out var shokoEpisodeID) ? RepoFactory.AnimeEpisode.GetByID(shokoEpisodeID) : null,
            _ when source == MetadataSource.AniDB => int.TryParse(id, out var anidbEpisodeID) ? RepoFactory.AniDB_Episode.GetByEpisodeID(anidbEpisodeID) : null,
            _ => ResolveThroughResolvers(source, MetadataEntityType.Episode, id) as IEpisode,
        };

    /// <summary>
    /// Resolve an entry of any other source through the metadata service's
    /// lookup: the plugin's own
    /// <see cref="Shoko.Abstractions.Metadata.Providers.IMetadataResolver"/> for the source and kind,
    /// then the core's metadata stores.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="type">The kind of entry.</param>
    /// <param name="id">The source's ID for it.</param>
    /// <returns>The entry, or <c>null</c> when nothing holds it.</returns>
    private IMetadata? ResolveThroughResolvers(MetadataSource source, MetadataEntityType type, string id)
        => _service.GetStoredEntity(source, type, id);

    /// <summary>
    /// Parse the composite ID the shoko and AniDB seasons are keyed by, which
    /// is the series' ID, the episode type and the season number.
    /// </summary>
    /// <param name="id">The stored season ID.</param>
    /// <returns>The parts, or <c>null</c> when the ID isn't one.</returns>
    private static (int ID, EpisodeType Type, int Number)? ParseEmbeddedSeasonID(string id)
        => id.Split(':') is not { Length: 3 } parts ||
            !int.TryParse(parts[0], out var seriesID) ||
            !Enum.TryParse<EpisodeType>(parts[1], true, out var episodeType) ||
            !int.TryParse(parts[2], out var seasonNumber)
                ? null
                : (seriesID, episodeType, seasonNumber);

    #endregion

    #region Resolution

    /// <summary>
    /// The regular episode at a number on a schedule's line: the one of the
    /// schedule's season, or its series, with that number. The schedule's
    /// episodes are gathered once per read.
    /// </summary>
    /// <param name="row">The schedule.</param>
    /// <param name="episodeNumber">The number on the line.</param>
    /// <returns>The episode, or <c>null</c> when none has the number, or two do.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is <c>null</c>.</exception>
    public IEpisode? GetScheduleEpisodeByNumber(AiringSchedule row, int episodeNumber)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!_scheduleEpisodesByNumber.TryGetValue(row.AiringScheduleID, out var byNumber))
        {
            var episodes = new Dictionary<int, IEpisode?>();
            foreach (var episode in AiringScheduleService.GetScheduleEpisodes(this, row))
            {
                if (episode.Type is not EpisodeType.Episode)
                    continue;

                // Two episodes sharing a number leave it unresolved rather than picking one.
                episodes[episode.EpisodeNumber] = episodes.ContainsKey(episode.EpisodeNumber) ? null : episode;
            }

            _scheduleEpisodesByNumber[row.AiringScheduleID] = byNumber = episodes;
        }

        return byNumber.GetValueOrDefault(episodeNumber);
    }

    /// <summary>
    /// The episode a stored airing is for: the pinned one, else the regular
    /// episode at its number on the line.
    /// </summary>
    /// <param name="row">The airing's schedule.</param>
    /// <param name="entry">The stored airing.</param>
    /// <returns>The episode, or <c>null</c> when the airing is unresolved.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> or <paramref name="entry"/> is <c>null</c>.</exception>
    public IEpisode? ResolveEpisode(AiringSchedule row, EpisodeAiring entry)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.IsPinned)
            return GetEpisode(entry.EpisodeSource, entry.EpisodeID);

        return entry.SequenceNumber is { } sequenceNumber ? GetScheduleEpisodeByNumber(row, row.FirstEpisodeNumber + sequenceNumber - 1) : null;
    }

    /// <summary>
    /// The AniDB anime and regular episode number a number on a schedule's
    /// line stands for, from the schedule's series alone, worked out once per
    /// schedule, number and read.
    /// </summary>
    /// <param name="row">The schedule.</param>
    /// <param name="episodeNumber">The number on the line.</param>
    /// <returns>The AniDB anime and episode number, or <c>null</c> when the series leads to none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is <c>null</c>.</exception>
    public (int AnimeID, int EpisodeNumber)? GetAnidbPosition(AiringSchedule row, int episodeNumber)
    {
        ArgumentNullException.ThrowIfNull(row);

        var key = (row.AiringScheduleID, episodeNumber);
        if (_anidbPositions.TryGetValue(key, out var position))
            return position;

        return _anidbPositions[key] = AiringScheduleService.FindAnidbPosition(this, row, episodeNumber);
    }

    /// <summary>
    /// The regular AniDB episode with a number in an anime, when AniDB lists
    /// it, from the anime's episodes gathered once per read.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime.</param>
    /// <param name="episodeNumber">The regular episode number.</param>
    /// <returns>The episode, or <c>null</c> when AniDB lists none.</returns>
    public IAnidbEpisode? GetAnidbEpisode(int anidbAnimeID, int episodeNumber)
    {
        if (!_anidbEpisodesByNumber.TryGetValue(anidbAnimeID, out var byNumber))
        {
            _anidbEpisodesByNumber[anidbAnimeID] = byNumber = RepoFactory.AniDB_Episode.GetByAnimeID(anidbAnimeID)
                .Where(episode => episode.EpisodeType is EpisodeType.Episode)
                .DistinctBy(episode => episode.EpisodeNumber)
                .ToDictionary(episode => episode.EpisodeNumber, IAnidbEpisode (episode) => episode);
        }

        return byNumber.GetValueOrDefault(episodeNumber);
    }

    /// <summary>
    /// The AniDB anime behind an ID, looked up once per read.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <returns>The anime, or <c>null</c> when it is not cached.</returns>
    public IAnidbAnime? GetAnidbAnime(int anidbAnimeID)
    {
        if (_anidbAnime.TryGetValue(anidbAnimeID, out var anime))
            return anime;

        return _anidbAnime[anidbAnimeID] = RepoFactory.AniDB_Anime.GetByAnimeID(anidbAnimeID);
    }

    /// <summary>
    /// The shoko series of an AniDB anime, looked up once per read.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <returns>The shoko series, or <c>null</c> when the anime is not in the collection.</returns>
    public IShokoSeries? GetShokoSeriesByAnimeID(int anidbAnimeID)
    {
        if (_shokoSeriesByAnimeID.TryGetValue(anidbAnimeID, out var series))
            return series;

        return _shokoSeriesByAnimeID[anidbAnimeID] = RepoFactory.AnimeSeries.GetByAnimeID(anidbAnimeID);
    }

    /// <summary>
    /// The stored airings a read gathers for a target, worked out once per
    /// target and read.
    /// </summary>
    /// <param name="target">The target.</param>
    /// <param name="episodeLinked">Whether the episode's links are followed.</param>
    /// <param name="positionLinked">Whether a place on a line is looked up on the schedules of other sources too.</param>
    /// <returns>Every stored airing of the target, each with its schedule.</returns>
    public IReadOnlyList<(AiringSchedule Schedule, EpisodeAiring Entry)> GetStoredAirings(AiringReadTarget target, bool episodeLinked, bool positionLinked)
    {
        var key = (TargetMemoKey.For(target), episodeLinked, positionLinked);
        if (_storedAirings.TryGetValue(key, out var airings))
            return airings;

        return _storedAirings[key] = AiringScheduleService.CollectStoredAirings(this, target, episodeLinked, positionLinked);
    }

    #endregion

    #region Links

    /// <summary>
    /// Every entity key an episode's airings can live under: the episode
    /// itself, the shoko episodes it belongs to, and their linked episodes.
    /// Each is visited once.
    /// </summary>
    /// <param name="episode">The episode to collect keys for.</param>
    /// <returns>The keys, with the episode's own first.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="episode"/> is <c>null</c>.</exception>
    public IReadOnlyList<(MetadataSource Source, string ID)> GetLinkedEpisodeKeys(IEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);

        var key = (episode.Source, episode.ID.ID);
        if (_linkedEpisodeKeys.TryGetValue(key, out var keys))
            return keys;

        var visited = new List<(MetadataSource Source, string ID)>();
        var seen = new HashSet<(MetadataSource, string)>();
        void Add(IEpisode entry)
        {
            if (seen.Add((entry.Source, entry.ID.ID)))
                visited.Add((entry.Source, entry.ID.ID));
        }

        Add(episode);
        var shokoEpisodes = episode is IShokoEpisode shokoEpisode ? [shokoEpisode] : episode.ShokoEpisodes;
        foreach (var entry in shokoEpisodes)
        {
            Add(entry);
            foreach (var linked in entry.LinkedEpisodes)
                Add(linked);
        }

        return _linkedEpisodeKeys[key] = visited;
    }

    /// <summary>
    /// The series links of an AniDB anime, on every source, read once per read.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime.</param>
    /// <returns>The links.</returns>
    public IReadOnlyList<IMetadataSeriesCrossReference> GetSeriesLinks(int anidbAnimeID)
    {
        if (_seriesLinks.TryGetValue(anidbAnimeID, out var links))
            return links;

        return _seriesLinks[anidbAnimeID] = _service.CrossReferences.GetSeriesLinks(anidbAnimeID);
    }

    /// <summary>
    /// The episode links of an AniDB anime, on every source, read once per read.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime.</param>
    /// <returns>The links.</returns>
    public IReadOnlyList<IMetadataEpisodeCrossReference> GetEpisodeLinks(int anidbAnimeID)
    {
        if (_episodeLinks.TryGetValue(anidbAnimeID, out var links))
            return links;

        return _episodeLinks[anidbAnimeID] = _service.CrossReferences.GetEpisodeLinksForSeries(anidbAnimeID);
    }

    /// <summary>
    /// The AniDB anime linked to a series of a plugin source, read once per read.
    /// </summary>
    /// <param name="source">The source of the series.</param>
    /// <param name="id">The ID of the series within its source.</param>
    /// <returns>The AniDB anime IDs, or none for a core source.</returns>
    public IReadOnlyList<int> GetLinkedAnimeIDs(MetadataSource source, string id)
    {
        if (_linkedAnimeIDs.TryGetValue((source, id), out var animeIDs))
            return animeIDs;

        return _linkedAnimeIDs[(source, id)] = source.IsCore || string.IsNullOrEmpty(id)
            ? []
            : [.. _service.CrossReferences.GetLinksTo(new(source, MetadataEntityType.Series, id)).Select(link => link.AnidbAnimeID).Distinct()];
    }

    /// <summary>
    /// How an AniDB anime's episodes number on a schedule, learned once per
    /// anime, schedule and read.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime.</param>
    /// <param name="row">The schedule.</param>
    /// <returns>The offset, or <c>null</c> when the links give no consistent one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is <c>null</c>.</exception>
    public AiringEpisodeOffset? GetEpisodeOffset(int anidbAnimeID, AiringSchedule row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var key = (anidbAnimeID, row.AiringScheduleID);
        if (_episodeOffsets.TryGetValue(key, out var offset))
            return offset;

        return _episodeOffsets[key] = AiringScheduleService.LearnEpisodeOffset(this, anidbAnimeID, row);
    }

    #endregion

    #region Collection

    /// <summary>
    /// What the collection filters need to know about the series behind an
    /// airing, resolved once per series and read. An unresolvable series is
    /// not fatal: it is simply not in the collection.
    /// </summary>
    /// <param name="airing">The airing.</param>
    /// <returns>The series' state.</returns>
    public AiringSeriesState GetSeriesState(IEpisodeAiring airing)
    {
        if (GetSeriesFor(airing) is not { } series)
            return AiringSeriesState.Unknown;

        if (_seriesStates.TryGetValue(series.ID, out var state))
            return state;

        var anidbAnimeID = series switch
        {
            IShokoSeries shokoSeries => shokoSeries.AnidbAnimeID,
            IAnidbAnime anidbAnime => anidbAnime.AnidbID,
            _ => (int?)null,
        };
        var anime = anidbAnimeID is { } animeID ? RepoFactory.AniDB_Anime.GetByAnimeID(animeID) : null;
        var localSeries = anidbAnimeID is { } seriesAnimeID ? RepoFactory.AnimeSeries.GetByAnimeID(seriesAnimeID) : null;
        return _seriesStates[series.ID] = new AiringSeriesState
        {
            Series = series,
            AnidbAnime = anime,
            IsRestricted = anime?.IsRestricted ?? series.Restricted,
            IsInCollection = localSeries is not null,
            AnidbAnimeID = anidbAnimeID,
        };
    }

    /// <summary>
    /// The AniDB anime of the Shoko series a filter passes for a user,
    /// evaluated once per read.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="user">The user, needed when the filter depends on one.</param>
    /// <exception cref="ArgumentNullException">
    /// The filter depends on the user and <paramref name="user"/> is <c>null</c>.
    /// </exception>
    /// <returns>The AniDB anime IDs.</returns>
    public IReadOnlySet<int> GetFilteredAnimeIDs(IFilter filter, IUser? user)
    {
        if (_filteredAnimeIDs is { } cached && ReferenceEquals(cached.Filter, filter))
            return cached.AnimeIDs;

        var animeIDs = _service.GetFilteredAnimeIDs(filter, user).ToHashSet();
        _filteredAnimeIDs = (filter, animeIDs);
        return animeIDs;
    }

    /// <summary>
    /// The series behind an airing: the shoko series where there is one, else
    /// whatever the airing or its schedule resolved to.
    /// </summary>
    /// <param name="airing">The airing.</param>
    /// <returns>The series, or <c>null</c> when none could be resolved.</returns>
    public static ISeries? GetSeriesFor(IEpisodeAiring airing)
        => (ISeries?)airing.ShokoEpisode?.Series ??
            airing.AnidbEpisode?.Series ??
            airing.ShokoSeries ??
            airing.AnidbAnime ??
            airing.Episode?.Series ??
            airing.Schedule?.Series;

    #endregion

    #region Anchors

    /// <summary>
    /// The earliest known real Original airing of a target, across every
    /// visible schedule of every entity linked to it, leaving out advance
    /// screenings and reruns. It is what a simulpub's estimates anchor on and
    /// what <see cref="IEpisodeAiring.OffsetFromOriginal"/> is measured from.
    /// </summary>
    /// <param name="target">The target.</param>
    /// <returns>The earliest Original airing, or <c>null</c> when none is known.</returns>
    public DateTime? GetFirstOriginalAiringAt(AiringReadTarget target)
    {
        var key = TargetMemoKey.For(target);
        if (_firstOriginalAirings.TryGetValue(key, out var firstAiring))
            return firstAiring;

        // Seed the entry first: resolving the target's links can come back
        // around to the same target, and an anchor is never a cycle.
        _firstOriginalAirings[key] = null;

        var earliest = default(DateTime?);
        foreach (var (schedule, row) in GetStoredAirings(target, episodeLinked: true, positionLinked: true))
        {
            if (row.Kind is not EpisodeAiringKind.Normal || row.AiredAt is not { } airedAt || earliest is { } current && airedAt >= current)
                continue;
            if (!IsProviderVisible(schedule.ProviderID))
                continue;
            if (!schedule.Tracks.Any(track => track.Kind is AiringKind.Original))
                continue;

            earliest = airedAt;
        }

        return _firstOriginalAirings[key] = earliest;
    }

    /// <summary>
    /// The earliest stored <see cref="EpisodeAiringKind.Normal"/> airing of a
    /// target on each schedule, across every entity linked to it, whatever the
    /// schedule's provider, channel or tracks.
    /// </summary>
    /// <param name="target">The target.</param>
    /// <returns>One entry per schedule with a normal airing of the target.</returns>
    public IReadOnlyList<(AiringSchedule Schedule, DateTime AiredAt)> GetFirstNormalAirings(AiringReadTarget target)
    {
        var key = TargetMemoKey.For(target);
        if (_firstNormalAirings.TryGetValue(key, out var known))
            return known;

        var earliest = new Dictionary<int, (AiringSchedule Schedule, DateTime AiredAt)>();
        foreach (var (schedule, row) in GetStoredAirings(target, episodeLinked: true, positionLinked: true))
        {
            if (row.Kind is not EpisodeAiringKind.Normal || row.AiredAt is not { } airedAt)
                continue;
            if (earliest.TryGetValue(row.AiringScheduleID, out var current) && current.AiredAt <= airedAt)
                continue;

            earliest[row.AiringScheduleID] = (schedule, airedAt);
        }

        return _firstNormalAirings[key] = [.. earliest.Values];
    }

    /// <summary>
    /// The AniDB air date an episode's estimates are measured from, which is
    /// the episode's own when it is an AniDB episode and its shoko episode's
    /// otherwise.
    /// </summary>
    /// <param name="episode">The episode, if it resolved at all.</param>
    /// <returns>The AniDB air date, or <c>null</c> when there is none.</returns>
    public DateTime? GetAnidbAirDate(IEpisode? episode)
    {
        if (episode is null)
            return null;

        var key = (episode.Source, episode.ID.ID);
        if (_anidbAirDates.TryGetValue(key, out var airDate))
            return airDate;

        return _anidbAirDates[key] = ResolveAnidbAirDate(episode);
    }

    /// <summary>
    /// Walk an episode for the AniDB air date its estimates are measured from,
    /// which for anything but an AniDB episode means a cross-reference walk.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>The AniDB air date, or <c>null</c> when there is none.</returns>
    private static DateTime? ResolveAnidbAirDate(IEpisode episode)
    {
        if (episode is IAnidbEpisode && episode.AirDate is { } ownAirDate)
            return ownAirDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        foreach (var shokoEpisode in episode is IShokoEpisode entry ? [entry] : episode.ShokoEpisodes)
            if (shokoEpisode.AnidbEpisode?.AirDate is { } airDate)
                return airDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        return episode.AirDate is { } fallback ? fallback.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : null;
    }

    /// <summary>
    /// The AniDB and shoko views of the episode an airing was resolved for, so
    /// one stored airing linked to two AniDB episodes comes back once per
    /// episode with the right one attached.
    /// </summary>
    /// <param name="episode">The episode the read ran for.</param>
    /// <returns>The AniDB episode and the shoko episode, either of which can be <c>null</c>.</returns>
    public (IAnidbEpisode? AnidbEpisode, IShokoEpisode? ShokoEpisode) GetEpisodeViews(IEpisode? episode)
    {
        if (episode is null)
            return (null, null);
        if (episode is IAnidbEpisode anidbEpisode)
            return (anidbEpisode, anidbEpisode.ShokoEpisodes.FirstOrDefault());

        var shokoEpisode = episode as IShokoEpisode ?? episode.ShokoEpisodes.FirstOrDefault();
        return (shokoEpisode?.AnidbEpisode, shokoEpisode);
    }

    #endregion

    #region Lengths

    /// <summary>
    /// How long an AniDB episode runs: its own length, else the median length
    /// of its anime's regular episodes, worked out once per anime.
    /// </summary>
    /// <param name="anidbEpisode">The AniDB episode, if any.</param>
    /// <returns>The length, or <c>null</c> when neither is known.</returns>
    public TimeSpan? GetEpisodeDuration(IAnidbEpisode? anidbEpisode)
    {
        if (anidbEpisode is null)
            return null;
        if (anidbEpisode.Runtime > TimeSpan.Zero)
            return anidbEpisode.Runtime;

        return GetUsualEpisodeDuration(anidbEpisode.AnidbAnimeID);
    }

    /// <summary>
    /// The median length of an AniDB anime's regular episodes, worked out once
    /// per anime.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime.</param>
    /// <returns>The length, or <c>null</c> when it is not known.</returns>
    public TimeSpan? GetUsualEpisodeDuration(int anidbAnimeID)
    {
        if (!_usualEpisodeLengths.TryGetValue(anidbAnimeID, out var usual))
            _usualEpisodeLengths[anidbAnimeID] = usual = AnidbAnimeCatalog.GetEpisodeDuration(RepoFactory.AniDB_Episode.GetByAnimeID(anidbAnimeID));

        return usual;
    }

    /// <summary>
    /// The first stored airing on a channel that starts after a point in time,
    /// on any of the channel's schedules. The channel's slots are gathered
    /// once per read and searched from then on.
    /// </summary>
    /// <param name="channelID">The channel.</param>
    /// <param name="after">The point in time.</param>
    /// <returns>The start of that airing, or <c>null</c> when there is none.</returns>
    public DateTime? GetNextChannelSlot(Guid channelID, DateTime after)
    {
        if (!_channelSlots.TryGetValue(channelID, out var slots))
        {
            slots = RepoFactory.AiringSchedule.GetByChannelID(channelID)
                .SelectMany(row => RepoFactory.EpisodeAiring.GetByScheduleID(row.AiringScheduleID))
                .Select(entry => entry.AiredAt)
                .OfType<DateTime>()
                .Distinct()
                .Order()
                .ToList();
            _channelSlots[channelID] = slots;
        }

        // The index of the first slot after the point, from the complement of a miss or past an exact hit.
        var index = slots.BinarySearch(after);
        index = index < 0 ? ~index : index + 1;
        return index < slots.Count ? slots[index] : null;
    }

    #endregion

    #region Reruns

    /// <summary>
    /// The kind an airing is read as: the provider's own, or
    /// <see cref="EpisodeAiringKind.DetectedRerun"/> for a
    /// <see cref="EpisodeAiringKind.Normal"/> airing on a schedule detected as
    /// a rerun.
    /// </summary>
    /// <param name="row">The airing's schedule.</param>
    /// <param name="kind">The kind the provider gave the airing.</param>
    /// <returns>The effective kind.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is <c>null</c>.</exception>
    public EpisodeAiringKind GetEffectiveKind(AiringSchedule row, EpisodeAiringKind kind)
    {
        ArgumentNullException.ThrowIfNull(row);

        return kind is EpisodeAiringKind.Normal && IsDetectedRerun(row) ? EpisodeAiringKind.DetectedRerun : kind;
    }

    /// <summary>
    /// Whether a schedule is detected as a rerun of an earlier run, worked out
    /// once per schedule and read.
    /// </summary>
    /// <param name="row">The schedule.</param>
    /// <returns><c>true</c> when the schedule's normal airings are reruns.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is <c>null</c>.</exception>
    public bool IsDetectedRerun(AiringSchedule row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (_detectedReruns.TryGetValue(row.AiringScheduleID, out var detected))
            return detected;

        return _detectedReruns[row.AiringScheduleID] = AiringScheduleService.DetectRerun(this, row);
    }

    /// <summary>
    /// Whether two schedules release anything in common, worked out once per
    /// pair and read.
    /// </summary>
    /// <param name="one">One schedule.</param>
    /// <param name="other">The other schedule.</param>
    /// <returns><c>true</c> when the two share a track.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="one"/> or <paramref name="other"/> is <c>null</c>.</exception>
    public bool SharesTrack(AiringSchedule one, AiringSchedule other)
    {
        ArgumentNullException.ThrowIfNull(one);
        ArgumentNullException.ThrowIfNull(other);

        var key = one.AiringScheduleID < other.AiringScheduleID
            ? (one.AiringScheduleID, other.AiringScheduleID)
            : (other.AiringScheduleID, one.AiringScheduleID);
        if (_sharedTracks.TryGetValue(key, out var shared))
            return shared;

        return _sharedTracks[key] = AiringScheduleService.HasMatchingTrack(one.Tracks, other.Tracks);
    }

    #endregion

    /// <summary>
    /// A target as a read memoizes it: by its episode's stored key rather than
    /// the episode object, so two lookups of one episode share an entry.
    /// </summary>
    /// <param name="Episode">The stored key of the target's episode, if any.</param>
    /// <param name="Key">The target's key.</param>
    private readonly record struct TargetMemoKey((MetadataSource Source, string ID)? Episode, AiringEpisodeKey Key)
    {
        /// <summary>
        /// The memo key of a target.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <returns>The memo key.</returns>
        public static TargetMemoKey For(AiringReadTarget target)
            => new(target.Episode is { } episode ? (episode.Source, episode.ID.ID) : null, target.Key);
    }
}
