using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Video.Release;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Settings;

namespace Shoko.Server.Services;

/// <summary>
/// Schedules primary AniDB metadata refreshes and delegates supplementary
/// metadata work after a release is saved, for any provider, and queues the
/// refresh of the entries of other sources a release links to directly.
/// Extracted from <c>AnidbReleaseProvider.OnReleaseSaved</c> so the logic
/// runs for all providers, not just AniDB.
/// </summary>
public class AnimeMetadataOrchestrator(
    ILogger<AnimeMetadataOrchestrator> logger,
    IAnidbService anidbService,
    SupplementaryMetadataScheduler supplementaryMetadataScheduler,
    AniDB_AnimeRepository anidbAnimeRepository,
    AniDB_AnimeUpdateRepository anidbAnimeUpdateRepository,
    AnimeSeriesRepository shokoSeriesRepository,
    IQueueScheduler scheduler,
    ISettingsProvider settingsProvider,
    Lazy<MetadataProviderScheduler> metadataScheduler,
    Lazy<IMetadataService> metadataService
)
{
    private IServerSettings Settings => settingsProvider.GetSettings();

    /// <summary>
    /// Schedules AniDB refresh and supplementary metadata for all anime IDs
    /// referenced in the supplied legacy cross-references.
    /// </summary>
    public async Task ScheduleForXrefs(IReadOnlyList<CrossRef_File_Episode> xrefs)
    {
        var animeIDs = xrefs
            .GroupBy(xref => xref.AnimeID)
            .ExceptBy([0], g => g.Key)
            .ToDictionary(
                g => g.Key,
                g =>
                    anidbAnimeRepository.GetByAnimeID(g.Key) is null ||
                    shokoSeriesRepository.GetByAnimeID(g.Key) is null ||
                    anidbAnimeUpdateRepository.GetByAnimeID(g.Key) is null
            );

        if (animeIDs.Count == 0)
            return;

        var refreshMethod = AnidbRefreshMethod.Default | AnidbRefreshMethod.CreateShokoSeries;
        if (Settings.AutoGroupSeries || Settings.AniDb.DownloadRelatedAnime)
            refreshMethod |= AnidbRefreshMethod.DownloadRelations;

        foreach (var (animeID, missingData) in animeIDs)
        {
            var update = anidbAnimeUpdateRepository.GetByAnimeID(animeID);
            var animeRecentlyUpdated = !missingData
                && update is not null
                && (DateTime.Now - update.UpdatedAt).TotalHours < Settings.AniDb.MinimumHoursToRedownloadAnimeInfo;

            if (missingData)
            {
                logger.LogInformation("Queuing immediate GET for AniDB_Anime: {AnimeID}", animeID);
                await anidbService.ScheduleRefreshOfAnimeByID(animeID, refreshMethod, prioritize: true);
            }
            else if (!animeRecentlyUpdated)
            {
                logger.LogInformation("Queuing GET for AniDB_Anime: {AnimeID}", animeID);
                await anidbService.ScheduleRefreshOfAnimeByID(animeID, refreshMethod);
            }

            await scheduler.RunAfterCurrent<RefreshAnimeStatsJob>(b => b.AnimeID = animeID);
        }

        await supplementaryMetadataScheduler.ScheduleForAnimes(animeIDs.Keys);
    }

    /// <summary>
    /// Queues the refresh of the entries of other sources than AniDB a saved
    /// release links to directly, from the provider refreshing each one's
    /// source and kind; a source no enabled provider refreshes, such as one
    /// served by a resolver, gets nothing queued. An entry not stored yet is
    /// queued ahead of the rest.
    /// </summary>
    /// <param name="crossReferences">The cross-references of the saved release.</param>
    public async Task ScheduleForCrossReferences(IEnumerable<IReleaseVideoCrossReference> crossReferences)
    {
        var resolve = (MetadataGuid id) => metadataService.Value.GetEntry(id);
        var targets = GetRefreshTargets(crossReferences, resolve, out var unknown);
        foreach (var entry in targets)
        {
            var stored = resolve(entry) is not null;
            if (!await metadataScheduler.Value.ScheduleRefreshForEntry(entry, prioritize: !stored).ConfigureAwait(false))
                logger.LogDebug("Not queuing a refresh of {Entry}, linked by a release: no enabled provider refreshes it.", entry);
        }

        foreach (var entry in unknown)
            logger.LogDebug("Not queuing a refresh for {Entry}, linked by a release: its series is not known yet.", entry);
    }

    /// <summary>
    /// The series and movies to refresh for the entries of other sources than
    /// AniDB some cross-references link to. A series or movie is refreshed
    /// itself, and an episode through its series: the one the cross-reference
    /// names on the same source, else the one its stored entry belongs to.
    /// </summary>
    /// <param name="crossReferences">The cross-references.</param>
    /// <param name="resolve">Resolves an entry, or gives <c>null</c>.</param>
    /// <param name="unknown">The episodes whose series is not known.</param>
    /// <returns>The entries to refresh, each once.</returns>
    internal static IReadOnlyList<MetadataGuid> GetRefreshTargets(
        IEnumerable<IReleaseVideoCrossReference> crossReferences,
        Func<MetadataGuid, IMetadata?> resolve,
        out IReadOnlyList<MetadataGuid> unknown
    )
    {
        var targets = new List<MetadataGuid>();
        var missing = new List<MetadataGuid>();
        foreach (var crossReference in crossReferences)
        {
            foreach (var id in crossReference.IDs.Where(id => id.Source != MetadataSource.AniDB))
            {
                MetadataGuid? target;
                if (id.EntityType == MetadataEntityType.Series || id.EntityType == MetadataEntityType.Movie)
                    target = id;
                else if (id.EntityType == MetadataEntityType.Episode)
                {
                    target = crossReference.IDs.FirstOrDefault(other => other.Source == id.Source && other.EntityType == MetadataEntityType.Series) ??
                        (resolve(id) as IEpisode)?.Series?.ID;
                    if (target is null)
                    {
                        missing.Add(id);
                        continue;
                    }
                }
                else
                    continue;

                if (!targets.Contains(target))
                    targets.Add(target);
            }
        }

        unknown = missing;
        return targets;
    }
}
