using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;

namespace Shoko.Server.Services;

/// <summary>
///   Asks the metadata providers for their part once AniDB knows about an
///   anime, source by source: a search by the source's auto-linker where the
///   anime is linked on nothing, a refresh from each enabled provider with a
///   linked entry due, and otherwise only the matching of the anime's
///   episodes again.
/// </summary>
/// <remarks>
///   Called after an AniDB refresh, unless it was asked to skip supplementary
///   updates, and after a release links a file to the anime. The search job
///   itself checks the auto-link setting and the anime's veto. A refresh
///   matches the episodes again once it is done.
/// </remarks>
/// <param name="providerManager">The registered providers.</param>
/// <param name="providerScheduler">Queues the provider jobs.</param>
/// <param name="metadataService">Tells whether the anime has a Shoko series yet.</param>
/// <param name="crossReferences">The links, to tell what the anime is linked to on each source.</param>
/// <param name="refreshState">When each linked entry was last refreshed.</param>
public class SupplementaryMetadataScheduler(
    IMetadataProviderManager providerManager,
    MetadataProviderScheduler providerScheduler,
    IMetadataService metadataService,
    IMetadataCrossReferenceStore crossReferences,
    IMetadataRefreshState refreshState
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
            var linked = crossReferences.GetLinkedEntries(anidbAnimeID, source);
            if (linked.Count is 0)
            {
                // Only an anime with a series to veto it.
                if (providers.Any(info => info.Source == source && info.AutoLink) && metadataService.GetShokoSeriesByAnidbID(anidbAnimeID) is not null)
                    await providerScheduler.ScheduleSearch(source, anidbAnimeID).ConfigureAwait(false);
                continue;
            }

            var refreshing = false;
            foreach (var info in providers.Where(info => info.Source == source && info.Enabled))
            {
                if (linked.Any(entry => IsDue(info, entry)))
                    refreshing |= await providerScheduler.ScheduleRefresh(info, anidbAnimeID).ConfigureAwait(false);
            }

            if (!refreshing)
                await providerScheduler.ScheduleEpisodeMatch(source, anidbAnimeID).ConfigureAwait(false);
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

    /// <summary>
    ///   Whether a provider would refresh a linked entry now: one of a kind it
    ///   refreshes and is enabled for, refreshed never or longer ago than
    ///   <see cref="MetadataRefreshState.FreshFor"/>.
    /// </summary>
    /// <param name="info">The provider.</param>
    /// <param name="entry">The linked series or film.</param>
    /// <returns><c>true</c> when the entry is due.</returns>
    private bool IsDue(MetadataProviderInfo info, MetadataGuid entry)
        => MetadataProviderScheduler.Refreshes(info.Provider, entry.EntityType) &&
            MetadataProviderScheduler.MayRefresh(info, entry.EntityType) &&
            !MetadataRefreshState.IsFresh(refreshState.GetLastRefreshedAt(entry));
}
