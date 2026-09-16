using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
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

    private readonly Dictionary<(DataSource Source, string ID), ISeries?> _series = [];

    private readonly Dictionary<(DataSource Source, string ID), ISeason?> _seasons = [];

    private readonly Dictionary<(DataSource Source, string ID), IReadOnlySet<(DataSource Source, string ID)>> _seasonEpisodeKeys = [];

    private readonly Dictionary<(DataSource Source, string ID), IEpisode?> _episodes = [];

    private readonly Dictionary<Guid, AiringScheduleProviderInfo?> _providers = [];

    private readonly Dictionary<Guid, IAiringChannel?> _channels = [];

    private readonly Dictionary<(DataSource Source, string ID), IReadOnlyList<(DataSource Source, string ID)>> _linkedEpisodeKeys = [];

    private readonly Dictionary<(DataSource Source, string ID), DateTime?> _firstOriginalAirings = [];

    private readonly Dictionary<(DataSource Source, string ID), DateTime?> _anidbAirDates = [];

    private AiringScheduleServiceSettings? _settings;

    /// <summary>
    /// Whether schedules whose provider is gone or disabled, and tracks of a
    /// disabled kind, are part of this read.
    /// </summary>
    public bool IncludeDisabled { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AiringReadContext"/> class.
    /// </summary>
    /// <param name="service">The service the read belongs to.</param>
    /// <param name="includeDisabled">Whether disabled providers and kinds are part of the read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="service"/> is <see langword="null"/>.</exception>
    public AiringReadContext(AiringScheduleService service, bool includeDisabled = false)
    {
        ArgumentNullException.ThrowIfNull(service);

        _service = service;
        IncludeDisabled = includeDisabled;
    }

    #region Settings

    /// <summary>
    /// The service's settings as they stood when the read started, loaded once
    /// rather than per airing: a load goes through the configuration provider,
    /// which is far too much to pay per ordered element.
    /// </summary>
    public AiringScheduleServiceSettings Settings => _settings ??= _service.LoadSettings();

    #endregion

    #region Views

    /// <summary>
    /// The view over a schedule row, shared by every airing of that schedule in
    /// this read.
    /// </summary>
    /// <param name="row">The schedule row.</param>
    /// <returns>The schedule view.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is <see langword="null"/>.</exception>
    public AiringScheduleView GetSchedule(AiringSchedule row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (_scheduleViews.TryGetValue(row.AiringScheduleID, out var view))
            return view;

        return _scheduleViews[row.AiringScheduleID] = new AiringScheduleView(this, row);
    }

    /// <summary>
    /// The view over the schedule a row belongs to, or <see langword="null"/>
    /// when the schedule is gone.
    /// </summary>
    /// <param name="scheduleID">The local ID of the schedule.</param>
    /// <returns>The schedule view, or <see langword="null"/>.</returns>
    public AiringScheduleView? GetSchedule(int scheduleID)
        => RepoFactory.AiringSchedule.GetByID(scheduleID) is { } row ? GetSchedule(row) : null;

    /// <summary>
    /// The estimate profile of a schedule, learned once per schedule and reused
    /// for every episode of it.
    /// </summary>
    /// <param name="row">The schedule row.</param>
    /// <returns>The schedule's profile.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is <see langword="null"/>.</exception>
    public AiringScheduleProfile GetProfile(AiringSchedule row)
        => _service.GetProfile(row, this);

    #endregion

    #region Providers & Channels

    /// <summary>
    /// The registered provider behind an ID, or <see langword="null"/> when the
    /// plugin that supplied it is gone.
    /// </summary>
    /// <param name="providerID">The ID of the provider.</param>
    /// <returns>The provider info, or <see langword="null"/>.</returns>
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
    /// <returns><see langword="true"/> when the provider's data is part of this read.</returns>
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
    /// <returns>The channel, or <see langword="null"/> when it is unknown.</returns>
    public IAiringChannel? GetChannel(Guid channelID)
    {
        if (_channels.TryGetValue(channelID, out var channel))
            return channel;

        return _channels[channelID] = RepoFactory.AiringChannel.GetByChannelID(channelID);
    }

    #endregion

    #region Entities

    /// <summary>
    /// The series behind a stored key, resolved once per read.
    /// </summary>
    /// <param name="source">The source of the series.</param>
    /// <param name="id">The ID of the series within its source.</param>
    /// <returns>The series, or <see langword="null"/> when it can't be resolved.</returns>
    public ISeries? GetSeries(DataSource source, string id)
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
    /// <returns>The season, or <see langword="null"/> when it can't be resolved.</returns>
    public ISeason? GetSeason(DataSource source, string id)
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
    public IReadOnlySet<(DataSource Source, string ID)> GetSeasonEpisodeKeys(DataSource source, string id)
    {
        if (_seasonEpisodeKeys.TryGetValue((source, id), out var keys))
            return keys;

        return _seasonEpisodeKeys[(source, id)] = GetSeason(source, id) is { } season
            ? season.Episodes.Select(episode => AiringScheduleService.GetEntityKey(episode)).ToHashSet()
            : new HashSet<(DataSource Source, string ID)>();
    }

    /// <summary>
    /// The episode behind a stored key, resolved once per read.
    /// </summary>
    /// <param name="source">The source of the episode.</param>
    /// <param name="id">The ID of the episode within its source.</param>
    /// <returns>The episode, or <see langword="null"/> when it can't be resolved.</returns>
    public IEpisode? GetEpisode(DataSource source, string id)
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
    /// <exception cref="ArgumentNullException"><paramref name="episode"/> is <see langword="null"/>.</exception>
    public void Remember(IEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);

        _episodes[(episode.Source, episode.ID.ToString())] = episode;
    }

    private ISeries? ResolveSeries(DataSource source, string id)
        => source switch
        {
            DataSource.Shoko => int.TryParse(id, out var shokoSeriesID) ? RepoFactory.AnimeSeries.GetByID(shokoSeriesID) : null,
            DataSource.AniDB => int.TryParse(id, out var anidbAnimeID) ? RepoFactory.AniDB_Anime.GetByAnimeID(anidbAnimeID) : null,
            DataSource.TMDB => int.TryParse(id, out var tmdbShowID) ? RepoFactory.TMDB_Show.GetByTmdbShowID(tmdbShowID) : null,
            DataSource.AniList => int.TryParse(id, out var anilistAnimeID) ? RepoFactory.Anilist_Anime.GetByAnilistAnimeID(anilistAnimeID) : null,
            _ => ResolveThroughResolvers(source, DataEntityType.Series, id) as ISeries,
        };

    private ISeason? ResolveSeason(DataSource source, string id)
        => source switch
        {
            DataSource.Shoko => ParseEmbeddedSeasonID(id) is not { } shoko || RepoFactory.AnimeSeries.GetByID(shoko.ID) is not { } shokoSeries
                ? null : new AnimeSeason(shokoSeries, shoko.Type, shoko.Number),
            DataSource.AniDB => ParseEmbeddedSeasonID(id) is not { } anidb || RepoFactory.AniDB_Anime.GetByAnimeID(anidb.ID) is not { } anidbAnime
                ? null : new AniDB_Season(anidbAnime, anidb.Type, anidb.Number),
            DataSource.TMDB => int.TryParse(id, out var tmdbSeasonID) ? RepoFactory.TMDB_Season.GetByTmdbSeasonID(tmdbSeasonID) : null,
            _ => ResolveThroughResolvers(source, DataEntityType.Season, id) as ISeason,
        };

    private IEpisode? ResolveEpisode(DataSource source, string id)
        => source switch
        {
            DataSource.Shoko => int.TryParse(id, out var shokoEpisodeID) ? RepoFactory.AnimeEpisode.GetByID(shokoEpisodeID) : null,
            DataSource.AniDB => int.TryParse(id, out var anidbEpisodeID) ? RepoFactory.AniDB_Episode.GetByEpisodeID(anidbEpisodeID) : null,
            DataSource.TMDB => int.TryParse(id, out var tmdbEpisodeID) ? RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(tmdbEpisodeID) : null,
            DataSource.AniList => int.TryParse(id, out var anilistEpisodeID) ? RepoFactory.Anilist_Episode.GetByAnilistEpisodeID(anilistEpisodeID) : null,
            _ => ResolveThroughResolvers(source, DataEntityType.Episode, id) as IEpisode,
        };

    private IMetadata? ResolveThroughResolvers(DataSource source, DataEntityType type, string id)
    {
        foreach (var resolver in _service.EntityResolvers)
        {
            try
            {
                if (resolver.GetEntity(source, type, id) is { } entity)
                    return entity;
            }
            catch
            {
                // A resolver that throws is a plugin bug, and an unresolved entity is not fatal:
                // the schedule or airing still comes back with what the provider sent.
            }
        }

        return null;
    }

    /// <summary>
    /// Parse the composite ID the shoko and AniDB seasons are keyed by, which
    /// is the series' ID, the episode type and the season number.
    /// </summary>
    /// <param name="id">The stored season ID.</param>
    /// <returns>The parts, or <see langword="null"/> when the ID isn't one.</returns>
    private static (int ID, EpisodeType Type, int Number)? ParseEmbeddedSeasonID(string id)
        => id.Split(':') is not { Length: 3 } parts ||
            !int.TryParse(parts[0], out var seriesID) ||
            !Enum.TryParse<EpisodeType>(parts[1], true, out var episodeType) ||
            !int.TryParse(parts[2], out var seasonNumber)
                ? null
                : (seriesID, episodeType, seasonNumber);

    #endregion

    #region Links

    /// <summary>
    /// Every entity key an episode's airings can live under: the episode
    /// itself, the shoko episodes it belongs to, their linked episodes, and
    /// whatever the registered resolvers link to it. Each is visited once.
    /// </summary>
    /// <param name="episode">The episode to collect keys for.</param>
    /// <returns>The keys, with the episode's own first.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="episode"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<(DataSource Source, string ID)> GetLinkedEpisodeKeys(IEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);

        var key = (episode.Source, episode.ID.ToString());
        if (_linkedEpisodeKeys.TryGetValue(key, out var keys))
            return keys;

        var visited = new List<(DataSource Source, string ID)>();
        var seen = new HashSet<(DataSource, string)>();
        void Add(IEpisode entry)
        {
            if (seen.Add((entry.Source, entry.ID.ToString())))
                visited.Add((entry.Source, entry.ID.ToString()));
        }

        Add(episode);
        var shokoEpisodes = episode is IShokoEpisode shokoEpisode ? [shokoEpisode] : episode.ShokoEpisodes;
        foreach (var entry in shokoEpisodes)
        {
            Add(entry);
            foreach (var linked in entry.LinkedEpisodes)
                Add(linked);

            foreach (var resolver in _service.EntityResolvers)
            {
                try
                {
                    foreach (var linked in resolver.GetLinkedEntities(entry).OfType<IEpisode>())
                        Add(linked);
                }
                catch
                {
                    // As above: a resolver that throws costs its own links, nothing else.
                }
            }
        }

        return _linkedEpisodeKeys[key] = visited;
    }

    #endregion

    #region Anchors

    /// <summary>
    /// The earliest known real Original airing of an episode, across every
    /// visible schedule of every entity linked to it. It is what a simulpub's
    /// estimates anchor on and what <see cref="IEpisodeAiring.OffsetFromOriginal"/>
    /// is measured from.
    /// </summary>
    /// <param name="source">The source of the episode.</param>
    /// <param name="id">The ID of the episode within its source.</param>
    /// <returns>The earliest Original airing, or <see langword="null"/> when none is known.</returns>
    public DateTime? GetFirstOriginalAiringAt(DataSource source, string id)
    {
        if (_firstOriginalAirings.TryGetValue((source, id), out var firstAiring))
            return firstAiring;

        // Seed the entry first: resolving the episode's links can come back
        // around to the same episode, and an anchor is never a cycle.
        _firstOriginalAirings[(source, id)] = null;

        var keys = GetEpisode(source, id) is { } episode ? GetLinkedEpisodeKeys(episode) : [(source, id)];
        var earliest = default(DateTime?);
        foreach (var (keySource, keyID) in keys)
        {
            foreach (var row in RepoFactory.EpisodeAiring.GetByEpisodeID(keySource, keyID))
            {
                if (row.AiredAt is not { } airedAt || earliest is { } current && airedAt >= current)
                    continue;
                if (RepoFactory.AiringSchedule.GetByID(row.AiringScheduleID) is not { } schedule)
                    continue;
                if (!IsProviderVisible(schedule.ProviderID))
                    continue;
                if (!schedule.Tracks.Any(track => track.Kind is AiringKind.Original))
                    continue;

                earliest = airedAt;
            }
        }

        return _firstOriginalAirings[(source, id)] = earliest;
    }

    /// <summary>
    /// The AniDB air date an episode's estimates are measured from, which is
    /// the episode's own when it is an AniDB episode and its shoko episode's
    /// otherwise.
    /// </summary>
    /// <param name="episode">The episode, if it resolved at all.</param>
    /// <returns>The AniDB air date, or <see langword="null"/> when there is none.</returns>
    public DateTime? GetAnidbAirDate(IEpisode? episode)
    {
        if (episode is null)
            return null;

        var key = (episode.Source, episode.ID.ToString());
        if (_anidbAirDates.TryGetValue(key, out var airDate))
            return airDate;

        return _anidbAirDates[key] = ResolveAnidbAirDate(episode);
    }

    /// <summary>
    /// Walk an episode for the AniDB air date its estimates are measured from,
    /// which for anything but an AniDB episode means a cross-reference walk.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>The AniDB air date, or <see langword="null"/> when there is none.</returns>
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
    /// <returns>The AniDB episode and the shoko episode, either of which can be <see langword="null"/>.</returns>
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
}
