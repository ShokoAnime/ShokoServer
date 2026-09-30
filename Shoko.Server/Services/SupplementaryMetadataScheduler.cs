using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Services;

/// <summary>
///   Asks the metadata providers for their part once AniDB knows about an
///   anime: a refresh from each enabled provider whose source the anime is
///   linked on, and a search by the source's auto-linker where it is linked
///   on nothing.
/// </summary>
/// <remarks>
///   Called after an AniDB refresh, unless it was asked to skip supplementary
///   updates, and after a release links a file to the anime. The search job
///   itself checks the auto-link setting and the anime's veto.
/// </remarks>
/// <param name="providerManager">The registered providers.</param>
/// <param name="providerScheduler">Queues the provider jobs.</param>
/// <param name="metadataService">Tells whether the anime has a Shoko series yet.</param>
public class SupplementaryMetadataScheduler(
    IMetadataProviderManager providerManager,
    MetadataProviderScheduler providerScheduler,
    IMetadataService metadataService
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
            var refreshed = false;
            foreach (var info in providers.Where(info => info.Source == source && info.Enabled))
                refreshed |= await providerScheduler.ScheduleRefresh(info, anidbAnimeID).ConfigureAwait(false);

            // Only an anime with a series to veto it.
            if (!refreshed && providers.Any(info => info.Source == source && info.AutoLink) && metadataService.GetShokoSeriesByAnidbID(anidbAnimeID) is not null)
                await providerScheduler.ScheduleSearch(source, anidbAnimeID).ConfigureAwait(false);
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
