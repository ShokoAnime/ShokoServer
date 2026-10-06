using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;

namespace Shoko.Server.Services;

/// <summary>
///   Asks the metadata providers for their part once AniDB knows about an
///   anime, source by source: a search by the source's auto-linker where the
///   anime is linked on nothing, and elsewhere a refresh of each linked
///   entry due, one job per entry, or the matching of the anime's episodes
///   again when no series is due.
/// </summary>
/// <remarks>
///   Called after an AniDB refresh, unless it was asked to skip supplementary
///   updates, and after a release links a file to the anime. The search job
///   itself checks the auto-link setting and the anime's veto. A series
///   refresh matches the episodes of every anime linked to it once it is done.
/// </remarks>
/// <param name="providerManager">The registered providers.</param>
/// <param name="providerScheduler">Queues the provider jobs.</param>
/// <param name="metadataService">Tells whether the anime has a Shoko series yet.</param>
/// <param name="crossReferences">The links, to tell what the anime is linked to on each source.</param>
public class SupplementaryMetadataScheduler(
    IMetadataProviderManager providerManager,
    MetadataProviderScheduler providerScheduler,
    IMetadataService metadataService,
    IMetadataCrossReferenceStore crossReferences
)
{
    /// <summary>
    ///   Queues the providers' jobs for one anime.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID. Nothing is queued for one below 1.</param>
    /// <returns>A task that completes once every job is queued.</returns>
    public async Task ScheduleForAnime(int anidbAnimeID)
    {
        if (anidbAnimeID <= 0)
            return;

        var providers = providerManager.MetadataProviders;
        foreach (var source in providers.Select(info => info.Source).Distinct())
        {
            if (crossReferences.GetLinkedEntries(anidbAnimeID, source).Count is 0)
            {
                // Only an anime with a series to veto it.
                if (providers.Any(info => info.Source == source && info.AutoLink) && metadataService.GetShokoSeriesByAnidbID(anidbAnimeID) is not null)
                    await providerScheduler.ScheduleSearch(source, anidbAnimeID).ConfigureAwait(false);
                continue;
            }

            await providerScheduler.ScheduleRefreshForAnime(anidbAnimeID, source).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///   Queues the providers' jobs for each anime in turn.
    /// </summary>
    /// <param name="anidbAnimeIDs">The AniDB anime IDs.</param>
    /// <returns>A task that completes once every job is queued.</returns>
    public async Task ScheduleForAnimes(IEnumerable<int> anidbAnimeIDs)
    {
        foreach (var animeID in anidbAnimeIDs)
            await ScheduleForAnime(animeID).ConfigureAwait(false);
    }
}
