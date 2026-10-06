using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.Utilities;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Settings;

namespace Shoko.Server.Actions;

/// <summary>
///   Refresh from AniDB the anime in the collection with an episode airing
///   within the airing schedule's <see cref="AiringScheduleServiceSettings.AiringSoonWindowHours"/>,
///   unless updated recently, so their other sources and episode matches
///   follow before the episode airs.
/// </summary>
/// <remarks>
///   Only queues the refreshes; the progress covers the queuing. A run while
///   AniDB's HTTP API bans us is skipped.
/// </remarks>
/// <param name="logger">Logs the run.</param>
/// <param name="settingsProvider">Holds the minimum time between two AniDB updates of an anime.</param>
/// <param name="airingSettings">Holds the window and whether date-only entries count.</param>
/// <param name="anidbService">Tells whether AniDB's HTTP API bans us.</param>
/// <param name="airingScheduleService">Finds the episodes airing soon.</param>
/// <param name="animeSeries">Tells which anime are in the collection.</param>
/// <param name="anidbAnimeUpdates">When each anime was last updated from AniDB.</param>
/// <param name="scheduler">Queues the refreshes.</param>
public sealed class RefreshAnimeAiringSoonAction(
    ILogger<RefreshAnimeAiringSoonAction> logger,
    ISettingsProvider settingsProvider,
    ConfigurationProvider<AiringScheduleServiceSettings> airingSettings,
    IAnidbService anidbService,
    IAiringScheduleService airingScheduleService,
    AnimeSeriesRepository animeSeries,
    AniDB_AnimeUpdateRepository anidbAnimeUpdates,
    IQueueScheduler scheduler
) : IScheduledAction
{
    #region Definition

    public string Name => "Refresh Anime Airing Soon";

    public string? Description
        => "Refreshes from AniDB the anime in the collection with an episode airing within the window set in the airing schedule settings "
            + "that were not updated recently, then their other metadata sources and episode matches.";

    public ActionCategory Category => ActionCategory.AniDB;

    public IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.Every(TimeSpan.FromHours(24))];

    public TimeSpan? MinimumInterval => TimeSpan.FromHours(1);

    #endregion

    #region Execution

    public Task<ActionValidationResult?> Validate(CancellationToken token)
        => Task.FromResult(
            anidbService.IsAnidbHttpBanned
                ? new ActionValidationResult("AniDB's HTTP API is banning us. Try again once the ban expires.")
                : null
        );

    public async Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        var minimumHours = settingsProvider.GetSettings().AniDb.MinimumHoursToRedownloadAnimeInfo;
        var minimumAge = GetMinimumAge(minimumHours);
        var settings = airingSettings.Load();
        var window = GetWindow(settings.AiringSoonWindowHours);
        var animeIDs = GetAnimeAiringSoon(DateTimeOffset.UtcNow, window, settings.AiringSoonIncludeDateOnly);
        var items = new ItemProgress(progress, animeIDs.Count);
        items.Report(0);

        var queued = 0;
        foreach (var animeID in animeIDs)
        {
            token.ThrowIfCancellationRequested();
            var updatedAt = anidbAnimeUpdates.GetByAnimeID(animeID)?.UpdatedAt;
            if (updatedAt is { } lastUpdate && DateTime.Now - lastUpdate < minimumAge)
            {
                logger.LogDebug("Not refreshing AniDB anime {AnimeID}, airing soon, as it was updated at {UpdatedAt}.", animeID, lastUpdate);
                items.Increment();
                continue;
            }

            logger.LogDebug(
                "Queuing a refresh of AniDB anime {AnimeID}, airing soon, last updated at {UpdatedAt}.",
                animeID,
                updatedAt?.ToString() ?? "never"
            );
            await scheduler.Enqueue<GetAniDBAnimeJob>(
                job =>
                {
                    job.AnimeID = animeID;
                    job.UseRemote = true;
                    job.PreferCacheOverRemote = false;
                    // The time check is the one above.
                    job.IgnoreTimeCheck = true;
                    job.IgnoreHttpBans = false;
                    job.SkipSupplementaryUpdate = false;
                },
                ct: token
            ).ConfigureAwait(false);
            queued++;
            items.Increment();
        }

        logger.LogInformation(
            "Found {Count} anime in the collection airing within {WindowHours} hours: queued {Queued} AniDB refreshes, "
                + "skipped {Skipped} updated within {MinimumHours} hours.",
            animeIDs.Count,
            window.TotalHours,
            queued,
            animeIDs.Count - queued,
            minimumAge.TotalHours
        );
    }

    #endregion

    #region Selection

    /// <summary>
    ///   The AniDB anime in the collection with an episode airing within
    ///   <paramref name="window"/> of <paramref name="from"/>, soonest first.
    /// </summary>
    /// <remarks>
    ///   A date-only entry, when included, airs at an unknown time on its UTC
    ///   date, so it counts when that day overlaps the window.
    /// </remarks>
    /// <param name="from">The start of the window.</param>
    /// <param name="window">How far ahead an airing counts.</param>
    /// <param name="includeDateOnly">Whether the date-only AniDB entries count.</param>
    /// <returns>The AniDB anime IDs.</returns>
    internal IReadOnlyList<int> GetAnimeAiringSoon(DateTimeOffset from, TimeSpan window, bool includeDateOnly)
    {
        // In UTC, the range read matches a date-only entry exactly when its day overlaps the range.
        from = from.ToUniversalTime();
        var options = new EpisodeAiringFilteringOptions
        {
            IncludeDateOnly = includeDateOnly,
            IncludeDelayedOriginalSlots = false,
        };
        var seen = new HashSet<int>();
        var animeIDs = new List<int>();
        foreach (var airing in airingScheduleService.GetAiringsInRange(from, from + window, options))
        {
            foreach (var animeID in GetAnidbAnimeIDs(airing))
            {
                if (seen.Add(animeID) && animeSeries.GetByAnimeID(animeID) is not null)
                    animeIDs.Add(animeID);
            }
        }

        return animeIDs;
    }

    /// <summary>
    ///   The AniDB anime an airing is for: its AniDB episode's, else its Shoko
    ///   episode's, else the one an unresolved airing's place stands for, and
    ///   every one linked to the series of the airing's episode or schedule.
    /// </summary>
    /// <param name="airing">The airing.</param>
    /// <returns>The AniDB anime IDs, possibly repeated.</returns>
    private static IEnumerable<int> GetAnidbAnimeIDs(IEpisodeAiring airing)
    {
        if (airing.AnidbEpisode is { } anidbEpisode)
            yield return anidbEpisode.AnidbAnimeID;
        else if (airing.ShokoEpisode is { } shokoEpisode)
            yield return shokoEpisode.Series.AnidbAnimeID;
        else if (airing.AnidbAnimeID is { } anidbAnimeID)
            yield return anidbAnimeID;

        var series = airing.Episode?.Series ?? airing.Schedule?.Series;
        if (series is null)
            yield break;

        foreach (var link in series.MetadataSeriesCrossReferences)
            yield return link.AnidbAnimeID;
    }

    /// <summary>
    ///   How far ahead an airing makes its anime due: the setting's hours,
    ///   kept from one hour to a week.
    /// </summary>
    /// <param name="hours">The window's hours, from the settings.</param>
    /// <returns>The window.</returns>
    internal static TimeSpan GetWindow(int hours)
        => TimeSpan.FromHours(
            Math.Clamp(
                hours,
                AiringScheduleServiceSettings.MinimumAiringSoonWindowHours,
                AiringScheduleServiceSettings.MaximumAiringSoonWindowHours
            )
        );

    /// <summary>
    ///   How long ago an anime must have been updated from AniDB to be
    ///   refreshed: the setting's hours, and at least one.
    /// </summary>
    /// <param name="minimumHours">The minimum hours between two AniDB updates of an anime, from the settings.</param>
    /// <returns>The minimum age.</returns>
    internal static TimeSpan GetMinimumAge(int minimumHours)
        => TimeSpan.FromHours(Math.Max(minimumHours, 1));

    #endregion
}
