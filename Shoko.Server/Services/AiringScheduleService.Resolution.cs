using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Server.Models.Airing;
using Shoko.Server.Repositories;
using Shoko.Server.Services.Airing;

namespace Shoko.Server.Services;

public partial class AiringScheduleService
{
    private int _listeningForResolutions;

    #region Episode Airings | Resolution

    /// <summary>
    /// Starts listening, once, to the episode, series and link changes that
    /// make stored airings resolve differently, so they can be announced.
    /// </summary>
    private void ListenForResolutions()
    {
        if (Interlocked.Exchange(ref _listeningForResolutions, 1) is 1)
            return;

        var metadata = metadataService.Value;
        metadata.EpisodeAdded += OnEpisodeChanged;
        metadata.EpisodeRemoved += OnEpisodeChanged;
        metadata.SeriesAdded += OnSeriesChanged;
        metadata.SeriesUpdated += OnSeriesChanged;
        metadata.SeriesRemoved += OnSeriesChanged;
        linkingService.Value.LinksChanged += OnLinksChanged;
    }

    /// <summary>
    /// Announces the airings an added or removed episode resolves, or stops
    /// resolving.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="eventArgs">The change.</param>
    private void OnEpisodeChanged(object? sender, EpisodeInfoUpdatedEventArgs eventArgs)
        => AnnounceResolutions(context => GetResolvingAirings(context, [eventArgs]));

    /// <summary>
    /// Announces the airings of a series that was added or removed, and those
    /// the episodes an update added or removed resolve, or stop resolving.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="eventArgs">The change.</param>
    private void OnSeriesChanged(object? sender, SeriesInfoUpdatedEventArgs eventArgs)
        => AnnounceResolutions(context =>
        {
            InvalidateForSeries(eventArgs.SeriesInfo);
            if (eventArgs.Reason is not (UpdateReason.Added or UpdateReason.Removed))
                return GetResolvingAirings(context, eventArgs.Episodes.Where(change => change.Reason is UpdateReason.Added or UpdateReason.Removed));

            var (source, id) = GetEntityKey(eventArgs.SeriesInfo);
            return RepoFactory.AiringSchedule.GetBySeriesID(source, id)
                .SelectMany(row => RepoFactory.EpisodeAiring.GetByScheduleID(row.AiringScheduleID).Select(entry => (row, entry)));
        });

    /// <summary>
    /// Announces the airings on the schedules of the series whose links to
    /// AniDB changed, since their AniDB anime and episodes follow the links.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="eventArgs">The change.</param>
    private void OnLinksChanged(object? sender, MetadataLinksChangedEventArgs eventArgs)
        => AnnounceResolutions(context =>
        {
            var seriesKeys = new HashSet<(MetadataSource Source, string ID)>();
            foreach (var change in eventArgs.Changes)
            {
                foreach (var providerID in new[] { change.ProviderID, change.PreviousProviderID })
                {
                    if (providerID is null || providerID.Source.IsCore)
                        continue;

                    if (providerID.EntityType == MetadataEntityType.Series)
                        seriesKeys.Add((providerID.Source, providerID.ID));
                    else if (providerID.EntityType == MetadataEntityType.Episode && context.GetEpisode(providerID.Source, providerID.ID) is { } episode)
                        seriesKeys.Add((episode.Source, episode.SeriesID.ID));
                    else if (providerID.EntityType == MetadataEntityType.Season && context.GetSeason(providerID.Source, providerID.ID) is { } season)
                        seriesKeys.Add((season.Source, season.SeriesID.ID));
                }
            }

            return seriesKeys
                .SelectMany(key => RepoFactory.AiringSchedule.GetBySeriesID(key.Source, key.ID))
                .SelectMany(row => RepoFactory.EpisodeAiring.GetByScheduleID(row.AiringScheduleID).Select(entry => (row, entry)));
        });

    /// <summary>
    /// The stored airings whose episode changes with some episodes being
    /// added or removed: the ones pinned to them, the ones at their
    /// numbers on their series' schedules, and the ones an
    /// <see cref="AiringEpisodeOffset"/> places on an AniDB one.
    /// </summary>
    /// <param name="context">The read the lookups belong to, made after the change.</param>
    /// <param name="changes">The episodes added or removed.</param>
    /// <returns>The airings, each with its schedule, possibly repeated.</returns>
    private static IEnumerable<(AiringSchedule Schedule, EpisodeAiring Entry)> GetResolvingAirings(
        AiringReadContext context,
        IEnumerable<EpisodeInfoUpdatedEventArgs> changes
    )
    {
        foreach (var change in changes)
        {
            var episode = change.EpisodeInfo;
            var (source, id) = GetEntityKey(episode);
            foreach (var entry in RepoFactory.EpisodeAiring.GetByEpisodeID(source, id))
                if (RepoFactory.AiringSchedule.GetByID(entry.AiringScheduleID) is { } row)
                    yield return (row, entry);

            if (episode.Type is not EpisodeType.Episode)
                continue;

            var places = RepoFactory.AiringSchedule.GetBySeriesID(source, episode.SeriesID.ID)
                .Select(row => (Row: row, Number: episode.EpisodeNumber));
            if (episode is IAnidbEpisode anidbEpisode)
                places = places.Concat(GetUnlinkedEpisodeSchedules(context, anidbEpisode).Select(place => (Row: place.Schedule, place.Number)));

            foreach (var (row, number) in places)
            {
                var sequenceNumber = number - row.FirstEpisodeNumber + 1;
                if (sequenceNumber < 1)
                    continue;

                foreach (var entry in RepoFactory.EpisodeAiring.GetByScheduleIDAndSequenceNumber(row.AiringScheduleID, sequenceNumber))
                    if (!entry.IsPinned)
                        yield return (row, entry);
            }
        }
    }

    /// <summary>
    /// Raises <see cref="AiringsUpdated"/> once per schedule for the airings a
    /// change made resolve differently, after dropping the schedules' learned
    /// profiles. A failure is logged, so it never fails the change itself.
    /// </summary>
    /// <param name="findAirings">Finds the airings, in a read made after the change.</param>
    private void AnnounceResolutions(Func<AiringReadContext, IEnumerable<(AiringSchedule Schedule, EpisodeAiring Entry)>> findAirings)
    {
        if (!_loaded)
            return;

        try
        {
            var context = new AiringReadContext(this, includeDisabled: true);
            var bySchedule = findAirings(context)
                .GroupBy(pair => pair.Schedule.AiringScheduleID)
                .Select(group => (Schedule: group.First().Schedule, Entries: group.Select(pair => pair.Entry).DistinctBy(entry => entry.EpisodeAiringID).ToList()))
                .ToList();
            foreach (var (row, entries) in bySchedule)
            {
                _profiles.TryRemove(row.AiringScheduleID, out _);
                var scheduleView = context.GetSchedule(row);
                AiringsUpdated?.Invoke(this, new EpisodeAiringsUpdatedEventArgs
                {
                    Reason = UpdateReason.Updated,
                    Schedule = scheduleView,
                    Updated = ToViews(context, scheduleView, entries),
                    IsResolution = true,
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to announce the airings an episode, series or link change resolved differently.");
        }
    }

    #endregion
}
