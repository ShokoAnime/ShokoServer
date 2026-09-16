using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Models.Airing;
using Shoko.Server.Repositories;
using Shoko.Server.Services.Airing;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Services;

public partial class AiringScheduleService
{
    #region Schedules | Writing

    /// <inheritdoc/>
    public IAiringSchedule AddOrUpdateSchedule(IAiringScheduleProvider provider, AiringScheduleData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(data.Series);

        var info = GetRegisteredProvider(provider, nameof(provider));
        var tracks = NormalizeTracks(info, data.Tracks, nameof(data));
        var channelID = ResolveChannelID(data.ChannelID, nameof(data));
        ValidateCoverage(data.FirstEpisodeNumber, data.LastEpisodeNumber, nameof(data));
        if (data.Season is { } season && (season.Source != data.Series.Source || season.SeriesID != data.Series.ID))
            throw new ArgumentException("The season does not belong to the series.", nameof(data));

        var seriesKey = GetEntityKey(data.Series);
        var seasonID = data.Season?.ID ?? string.Empty;
        var key = string.IsNullOrWhiteSpace(data.Key) ? AiringScheduleUtility.GetDerivedScheduleKey(channelID, tracks) : data.Key.Trim();
        var timeZoneID = NormalizeTimeZone(data.TimeZone);
        var scheduleID = AiringScheduleUtility.GetScheduleID(info.ID, seriesKey.Source, seriesKey.ID, seasonID, key);
        var row = RepoFactory.AiringSchedule.GetByScheduleID(scheduleID);
        var reason = UpdateReason.Added;
        if (row is null)
        {
            row = new AiringSchedule()
            {
                ProviderID = info.ID,
                SeriesSource = seriesKey.Source,
                SeriesID = seriesKey.ID,
                SeasonID = seasonID,
                Key = key,
                ChannelID = channelID,
                CreatedAt = DateTime.UtcNow,
            };
        }
        else
        {
            if (row.ProviderID != info.ID)
                throw new ArgumentException("The schedule is owned by another provider.", nameof(provider));
            // A channel change is a different schedule, never an update to this one.
            if (row.ChannelID != channelID)
                throw new ArgumentException("The schedule already exists on another channel.", nameof(data));

            reason = UpdateReason.Updated;
        }

        row.ProviderName = info.Name;
        row.Tracks = tracks;
        row.FirstEpisodeNumber = data.FirstEpisodeNumber;
        row.LastEpisodeNumber = data.LastEpisodeNumber;
        row.IsFinished = data.IsFinished;
        row.TimeZoneID = timeZoneID;
        row.Url = string.IsNullOrWhiteSpace(data.Url) ? null : data.Url.Trim();
        row.LastUpdatedAt = DateTime.UtcNow;
        RepoFactory.AiringSchedule.Save(row);
        InvalidateProfilesForSeries(row.SeriesSource, row.SeriesID);

        var view = new AiringReadContext(this, includeDisabled: true).GetSchedule(row);
        ScheduleUpdated?.Invoke(this, new AiringScheduleEventArgs { Reason = reason, Schedule = view });
        return view;
    }

    /// <inheritdoc/>
    public IAiringSchedule UpdateSchedule(IAiringScheduleProvider provider, IAiringSchedule schedule, AiringScheduleUpdateData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var info = GetRegisteredProvider(provider, nameof(provider));
        var row = GetOwnedSchedule(info, schedule, nameof(schedule));
        if (data.Tracks is { } tracks)
            row.Tracks = NormalizeTracks(info, tracks, nameof(data));
        if (data.HasFirstEpisodeNumberSet)
            row.FirstEpisodeNumber = data.FirstEpisodeNumber;
        if (data.HasLastEpisodeNumberSet)
            row.LastEpisodeNumber = data.LastEpisodeNumber;
        ValidateCoverage(row.FirstEpisodeNumber, row.LastEpisodeNumber, nameof(data));
        if (data.IsFinished is { } isFinished)
            row.IsFinished = isFinished;
        if (data.HasTimeZoneSet)
            row.TimeZoneID = NormalizeTimeZone(data.TimeZone);
        if (data.HasUrlSet)
            row.Url = string.IsNullOrWhiteSpace(data.Url) ? null : data.Url.Trim();

        row.ProviderName = info.Name;
        row.LastUpdatedAt = DateTime.UtcNow;
        RepoFactory.AiringSchedule.Save(row);
        InvalidateProfilesForSeries(row.SeriesSource, row.SeriesID);

        var view = new AiringReadContext(this, includeDisabled: true).GetSchedule(row);
        ScheduleUpdated?.Invoke(this, new AiringScheduleEventArgs { Reason = UpdateReason.Updated, Schedule = view });
        return view;
    }

    /// <inheritdoc/>
    public bool RemoveSchedule(IAiringScheduleProvider provider, IAiringSchedule schedule)
    {
        var info = GetRegisteredProvider(provider, nameof(provider));
        var row = GetOwnedSchedule(info, schedule, nameof(schedule));
        return RemoveSchedule(row);
    }

    /// <inheritdoc/>
    public int RemoveSchedulesForSeries(IAiringScheduleProvider provider, ISeries series)
    {
        ArgumentNullException.ThrowIfNull(series);

        var info = GetRegisteredProvider(provider, nameof(provider));
        var seriesKey = GetEntityKey(series);
        var removed = 0;
        foreach (var row in RepoFactory.AiringSchedule.GetBySeriesID(seriesKey.Source, seriesKey.ID).Where(entry => entry.ProviderID == info.ID))
            if (RemoveSchedule(row))
                removed++;

        return removed;
    }

    /// <summary>
    /// Delete a schedule and every airing on it. An explicit removal is not a
    /// hiatus, so nothing is kept behind.
    /// </summary>
    /// <param name="row">The schedule to remove.</param>
    /// <returns><see langword="true"/> when the schedule was there to remove.</returns>
    private bool RemoveSchedule(AiringSchedule row)
    {
        var context = new AiringReadContext(this, includeDisabled: true);
        var view = context.GetSchedule(row);
        var airings = RepoFactory.EpisodeAiring.GetByScheduleID(row.AiringScheduleID);
        var airingViews = airings
            .Select(IEpisodeAiring (airing) => new EpisodeAiringView(context, view, airing))
            .ToList();
        foreach (var airing in airings)
            ForgetAiringID(airing);
        if (airings.Count > 0)
            RepoFactory.EpisodeAiring.Delete(airings);

        RepoFactory.AiringSchedule.Delete(row);
        _profiles.TryRemove(row.AiringScheduleID, out _);
        InvalidateProfilesForSeries(row.SeriesSource, row.SeriesID);

        if (airingViews.Count > 0)
            AiringsUpdated?.Invoke(this, new EpisodeAiringsUpdatedEventArgs { Reason = UpdateReason.Removed, Schedule = view, Airings = airingViews });
        ScheduleUpdated?.Invoke(this, new AiringScheduleEventArgs { Reason = UpdateReason.Removed, Schedule = view });
        return true;
    }

    #endregion

    #region Schedules | Reading

    /// <inheritdoc/>
    public IAiringSchedule? GetScheduleByID(Guid scheduleID)
        => RepoFactory.AiringSchedule.GetByScheduleID(scheduleID) is { } row
            ? new AiringReadContext(this, includeDisabled: true).GetSchedule(row)
            : null;

    /// <inheritdoc/>
    public IReadOnlyList<IAiringSchedule> GetSchedulesForSeries(ISeries series, AiringScheduleFilteringOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        options ??= new AiringScheduleFilteringOptions();
        var context = new AiringReadContext(this, options.IncludeDisabled);
        var seriesKey = GetEntityKey(series);
        var rows = RepoFactory.AiringSchedule.GetBySeriesID(seriesKey.Source, seriesKey.ID).ToList();
        if (options.LinkedEntitySchedules ?? series is IShokoSeries)
            foreach (var shokoSeries in series is IShokoSeries own ? [own] : series.ShokoSeries)
                rows.AddRange(GetLinkedSeriesSchedules(shokoSeries, seriesKey));

        return FilterSchedules(context, rows, options);
    }

    /// <inheritdoc/>
    public IReadOnlyList<IAiringSchedule> GetSchedulesForSeason(ISeason season, AiringScheduleFilteringOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(season);
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        options ??= new AiringScheduleFilteringOptions();
        var context = new AiringReadContext(this, options.IncludeDisabled);
        var rows = RepoFactory.AiringSchedule
            .GetBySeriesIDAndSeasonID(season.Source, season.SeriesID.ToString(), season.ID)
            .ToList();
        if (options.LinkedEntitySchedules ?? season is IShokoSeason)
            foreach (var shokoSeason in season is IShokoSeason own ? [own] : season.Series?.ShokoSeries.SelectMany(s => s.Seasons).OfType<IShokoSeason>() ?? [])
                rows.AddRange(GetLinkedSeasonSchedules(shokoSeason, season));

        return FilterSchedules(context, rows, options);
    }

    /// <inheritdoc/>
    public IReadOnlyList<IAiringSchedule> GetSchedulesForProvider(Guid providerID, AiringScheduleFilteringOptions? options = null)
    {
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        options ??= new AiringScheduleFilteringOptions();
        var context = new AiringReadContext(this, options.IncludeDisabled);
        return FilterSchedules(context, RepoFactory.AiringSchedule.GetByProviderID(providerID), options);
    }

    /// <inheritdoc/>
    public IReadOnlyList<IAiringSchedule> GetSchedulesForChannel(Guid channelID, AiringScheduleFilteringOptions? options = null)
    {
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        options ??= new AiringScheduleFilteringOptions();
        var context = new AiringReadContext(this, options.IncludeDisabled);
        return FilterSchedules(context, RepoFactory.AiringSchedule.GetByChannelID(channelID), options);
    }

    /// <summary>
    /// The schedules of a shoko series' linked series that actually belong to
    /// it. One TMDB show can be linked to several shoko series, so a schedule
    /// only counts when it has an airing on an episode linked to this series,
    /// or it narrows to one of this series' linked seasons.
    /// </summary>
    /// <param name="shokoSeries">The shoko series the read is for.</param>
    /// <param name="ownKey">The key of the entity the read started from, which is never walked twice.</param>
    /// <returns>The schedules that belong through a link.</returns>
    private IEnumerable<AiringSchedule> GetLinkedSeriesSchedules(IShokoSeries shokoSeries, (DataSource Source, string ID) ownKey)
    {
        var seen = new HashSet<(DataSource, string)> { ownKey };
        var linkedSeries = new List<(DataSource Source, string ID)>();
        void AddSeries(ISeries series)
        {
            var key = GetEntityKey(series);
            if (seen.Add(key))
                linkedSeries.Add(key);
        }

        AddSeries(shokoSeries);
        foreach (var series in shokoSeries.LinkedSeries)
            AddSeries(series);
        foreach (var series in GetResolverLinks<ISeries>(shokoSeries))
            AddSeries(series);

        var episodeKeys = shokoSeries.Episodes
            .SelectMany(episode => episode.LinkedEpisodes.Select(GetEntityKey).Prepend(GetEntityKey(episode)))
            .ToHashSet();
        var seasonKeys = shokoSeries.Seasons
            .SelectMany(season => season.LinkedSeasons.Select(GetEntityKey).Prepend(GetEntityKey(season)))
            .ToHashSet();
        foreach (var (source, id) in linkedSeries)
        {
            foreach (var row in RepoFactory.AiringSchedule.GetBySeriesID(source, id))
            {
                if (!string.IsNullOrEmpty(row.SeasonID) && seasonKeys.Contains((row.SeriesSource, row.SeasonID)))
                {
                    yield return row;
                    continue;
                }

                if (RepoFactory.EpisodeAiring.GetByScheduleID(row.AiringScheduleID)
                    .Any(airing => episodeKeys.Contains((airing.EpisodeSource, airing.EpisodeID))))
                    yield return row;
            }
        }
    }

    /// <summary>
    /// The schedules of a shoko season's linked seasons, plus the schedules of
    /// the linked series that have an airing on one of the season's linked
    /// episodes.
    /// </summary>
    /// <param name="shokoSeason">The shoko season the read is for.</param>
    /// <param name="ownSeason">The season the read started from, which is never walked twice.</param>
    /// <returns>The schedules that belong through a link.</returns>
    private IEnumerable<AiringSchedule> GetLinkedSeasonSchedules(IShokoSeason shokoSeason, ISeason ownSeason)
    {
        var ownKey = GetEntityKey(ownSeason);
        var episodeKeys = shokoSeason.Episodes
            .SelectMany(episode => episode.LinkedEpisodes.Select(GetEntityKey).Prepend(GetEntityKey(episode)))
            .ToHashSet();
        var seen = new HashSet<(DataSource, string)> { ownKey };
        foreach (var season in shokoSeason.LinkedSeasons.Prepend(shokoSeason).Concat(GetResolverLinks<ISeason>(shokoSeason)))
        {
            var key = GetEntityKey(season);
            if (!seen.Add(key))
                continue;

            foreach (var row in RepoFactory.AiringSchedule.GetBySeriesIDAndSeasonID(season.Source, season.SeriesID.ToString(), season.ID))
                yield return row;

            // A series-level schedule of the linked entity still belongs to this
            // season when its airings land on the season's own episodes.
            foreach (var row in RepoFactory.AiringSchedule.GetBySeriesIDAndSeasonID(season.Source, season.SeriesID.ToString(), null))
                if (RepoFactory.EpisodeAiring.GetByScheduleID(row.AiringScheduleID)
                    .Any(airing => episodeKeys.Contains((airing.EpisodeSource, airing.EpisodeID))))
                    yield return row;
        }
    }

    /// <summary>
    /// Run the schedule filters over a set of rows and hand back the views that
    /// survived, in a stable order.
    /// </summary>
    /// <param name="context">The read the views belong to.</param>
    /// <param name="rows">The candidate rows, which may repeat.</param>
    /// <param name="options">The filters to apply.</param>
    /// <returns>The matching schedules.</returns>
    private List<IAiringSchedule> FilterSchedules(AiringReadContext context, IEnumerable<AiringSchedule> rows, AiringScheduleFilteringOptions options)
        => rows
            .DistinctBy(row => row.AiringScheduleID)
            .Where(row => options.IncludeSeasonSchedules || string.IsNullOrEmpty(row.SeasonID))
            .Where(row => options.ProviderID is not { } providerID || row.ProviderID == providerID)
            .Where(row => options.ChannelIDs is not { } channelIDs || (row.ChannelID is { } channelID && channelIDs.Contains(channelID)))
            .Where(row => options.IncludeDisabled || context.IsProviderVisible(row.ProviderID))
            .Select(context.GetSchedule)
            .Where(view => options.IncludeDisabled || view.HasVisibleTracks)
            .Where(view => options.Kind is not { } kind || view.Tracks.Any(track => track.Kind == kind))
            .Where(view => options.Language is not { } language || view.Tracks.Any(track => track.Language == language))
            .OrderBy(view => view.Provider?.Priority ?? int.MaxValue)
            .ThenBy(view => view.ProviderName, StringComparer.Ordinal)
            .ThenBy(view => view.Channel?.Name ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(view => view.Key, StringComparer.Ordinal)
            .Select(IAiringSchedule (view) => view)
            .ToList();

    #endregion

    #region Schedules | Helpers

    /// <summary>
    /// The stored schedule a provider handed back, checked against the owner
    /// stored on it.
    /// </summary>
    /// <param name="info">The registered provider making the change.</param>
    /// <param name="schedule">The schedule the provider handed in.</param>
    /// <param name="paramName">The name of the argument the schedule arrived as.</param>
    /// <returns>The stored schedule.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schedule"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The schedule is unknown, or owned by another provider.</exception>
    private static AiringSchedule GetOwnedSchedule(AiringScheduleProviderInfo info, IAiringSchedule schedule, string paramName)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var row = RepoFactory.AiringSchedule.GetByScheduleID(schedule.ID)
            ?? throw new ArgumentException($"Unknown schedule: '{schedule.ID}'", paramName);
        if (row.ProviderID != info.ID)
            throw new ArgumentException("The schedule is owned by another provider.", paramName);

        return row;
    }

    /// <summary>
    /// Collapse a submitted track set to what is stored: no duplicates, in a
    /// stable order, and nothing of a kind the provider doesn't declare.
    /// </summary>
    /// <param name="info">The provider submitting the tracks.</param>
    /// <param name="tracks">The submitted tracks.</param>
    /// <param name="paramName">The name of the argument the tracks arrived in.</param>
    /// <returns>The tracks to store.</returns>
    /// <exception cref="ArgumentException">There are no tracks, or one is of a kind the provider doesn't declare.</exception>
    private static List<AiringTrackData> NormalizeTracks(AiringScheduleProviderInfo info, IReadOnlyList<AiringTrackData>? tracks, string paramName)
    {
        if (tracks is null || tracks.Count is 0)
            throw new ArgumentException("A schedule needs at least one track.", paramName);

        var available = info.Provider.AvailableKinds ?? AllKinds;
        var normalized = new List<AiringTrackData>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var track in tracks)
        {
            if (track is null)
                continue;
            if (!available.Contains(track.Kind))
                throw new ArgumentException($"The provider does not declare the kind '{track.Kind}'.", paramName);

            var languageCode = string.IsNullOrWhiteSpace(track.LanguageCode) ? "unk" : track.LanguageCode.Trim().ToLowerInvariant();
            var countryCode = string.IsNullOrWhiteSpace(track.CountryCode) ? null : track.CountryCode.Trim().ToUpperInvariant();
            if (!seen.Add($"{track.Kind}/{languageCode}/{countryCode}"))
                continue;

            normalized.Add(new AiringTrackData(track.Kind, languageCode, countryCode));
        }

        if (normalized.Count is 0)
            throw new ArgumentException("A schedule needs at least one track.", paramName);

        return normalized
            .OrderBy(track => track.Kind)
            .ThenBy(track => track.LanguageCode, StringComparer.Ordinal)
            .ThenBy(track => track.CountryCode ?? string.Empty, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Check that a submitted channel is one that was registered first.
    /// </summary>
    /// <param name="channelID">The submitted channel ID.</param>
    /// <param name="paramName">The name of the argument the channel arrived in.</param>
    /// <returns>The channel ID, or <see langword="null"/> when the schedule has none.</returns>
    /// <exception cref="ArgumentException">The channel isn't registered.</exception>
    private static Guid? ResolveChannelID(Guid? channelID, string paramName)
    {
        if (channelID is not { } id)
            return null;
        if (RepoFactory.AiringChannel.GetByChannelID(id) is null)
            throw new ArgumentException($"Unregistered channel: '{id}'", paramName);

        return id;
    }

    /// <summary>
    /// Check that a coverage range makes sense.
    /// </summary>
    /// <param name="firstEpisodeNumber">The first episode the schedule covers.</param>
    /// <param name="lastEpisodeNumber">The last episode the schedule covers.</param>
    /// <param name="paramName">The name of the argument the range arrived in.</param>
    /// <exception cref="ArgumentException">The first episode is after the last.</exception>
    private static void ValidateCoverage(int? firstEpisodeNumber, int? lastEpisodeNumber, string paramName)
    {
        if (firstEpisodeNumber is { } first && lastEpisodeNumber is { } last && first > last)
            throw new ArgumentException("The first episode of the coverage is after the last.", paramName);
    }

    /// <summary>
    /// The stored key of a series.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The source and ID it is stored under.</returns>
    internal static (DataSource Source, string ID) GetEntityKey(ISeries series)
        => (series.Source, series.ID.ToString());

    /// <summary>
    /// The stored key of a season.
    /// </summary>
    /// <param name="season">The season.</param>
    /// <returns>The source and ID it is stored under.</returns>
    internal static (DataSource Source, string ID) GetEntityKey(ISeason season)
        => (season.Source, season.ID);

    /// <summary>
    /// The stored key of an episode.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>The source and ID it is stored under.</returns>
    internal static (DataSource Source, string ID) GetEntityKey(IEpisode episode)
        => (episode.Source, episode.ID.ToString());

    /// <summary>
    /// The entities a plugin resolver links to a shoko entity, without letting
    /// a resolver that throws take the read with it.
    /// </summary>
    /// <typeparam name="TEntity">The kind of entity wanted.</typeparam>
    /// <param name="shokoEntity">The shoko entity to follow links from.</param>
    /// <returns>The linked entities of that kind.</returns>
    private List<TEntity> GetResolverLinks<TEntity>(IMetadata shokoEntity) where TEntity : class, IMetadata
    {
        var entities = new List<TEntity>();
        foreach (var resolver in _resolvers)
        {
            try
            {
                entities.AddRange(resolver.GetLinkedEntities(shokoEntity).OfType<TEntity>());
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Entity resolver {Resolver} threw while following links.", resolver.Name);
            }
        }

        return entities;
    }

    #endregion
}
