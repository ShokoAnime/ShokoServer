using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   Asks one provider to work out what an anime is on its source, and links
///   the candidates it takes.
/// </summary>
/// <remarks>
///   One job type per provider. Only the source's configured auto-linker is asked.
///   A scheduled search respects the auto-link setting, the anime's veto and existing
///   links; a forced one skips the first two. One a person asked for replaces every
///   link the anime has on the source. Nothing changes when the provider takes nothing
///   or fails; otherwise a refresh of what the anime now links to is queued.
/// </remarks>
/// <typeparam name="TProvider">The provider to ask.</typeparam>
[DatabaseRequired]
[MetadataProviderJob]
[JobKeyGroup(JobKeyGroup.Metadata)]
[JobPriority(Default = 10, Prioritized = 60)]
public class SearchMetadataJob<TProvider>(
    IMetadataProviderManager providerManager,
    IMetadataCrossReferenceStore crossReferences,
    IMetadataService metadataService,
    MetadataLinkingService linkingService,
    MetadataProviderScheduler providerScheduler,
    IJobCancellationAccessor cancellationAccessor
) : BaseJob, IMetadataSearchJob where TProvider : class, IMetadataAutoLinkingProvider
{
    #region Properties

    private MetadataProviderInfo? _providerInfo;

    /// <summary>
    ///   The AniDB anime to work out.
    /// </summary>
    public int AnimeID { get; set; }

    /// <summary>
    ///   Whether the search ignores the auto-link setting and the anime's
    ///   veto, and refreshes what it links however fresh it is.
    /// </summary>
    public bool Force { get; set; }

    /// <summary>
    ///   Whether a person asked for this one anime, which also searches an
    ///   anime already linked and replaces every link it has on the source
    ///   with what is taken, verified and episode links included.
    /// </summary>
    public bool Replace { get; set; }

    /// <inheritdoc />
    public override string TypeName => "Search Metadata";

    /// <inheritdoc />
    public override string Title => "Searching For Metadata Links";

    /// <inheritdoc />
    public override Dictionary<string, object> Details => _providerInfo is { } info
        ? new() { ["Provider"] = info.Name, ["Source"] = info.Source.Name, ["AnimeID"] = AnimeID }
        : new() { ["Provider"] = typeof(TProvider).Name, ["AnimeID"] = AnimeID };

    /// <inheritdoc />
    public override void PostInit()
        => _providerInfo = MetadataProviderJobContext.Find<TProvider>(providerManager);

    #endregion

    #region Execution

    /// <inheritdoc />
    public override async Task Execute()
    {
        if (AnimeID <= 0 || MetadataProviderJobContext.Resolve<TProvider>(providerManager, _logger) is not { } context)
            return;

        var (info, provider) = context;
        var source = info.Source;
        if (!info.IsAutoLinker)
        {
            _logger.LogDebug("Not searching {Source} for anime {AnimeID}: {Provider} is not its auto-linker.", source, AnimeID, info.Name);
            return;
        }

        if (!provider.IsConfigured)
        {
            _logger.LogDebug("Not searching {Source} for anime {AnimeID}: {Provider} is not configured.", source, AnimeID, info.Name);
            return;
        }

        if (!Force && !Replace)
        {
            if (!info.AutoLink)
            {
                _logger.LogDebug("Not searching {Source} for anime {AnimeID}: it does not auto-link.", source, AnimeID);
                return;
            }

            if (metadataService.GetShokoSeriesByAnidbID(AnimeID) is { } series && series.IsAutoLinkingDisabled(source))
            {
                _logger.LogDebug("Not searching {Source} for anime {AnimeID}: the anime is left alone.", source, AnimeID);
                return;
            }
        }

        // An episode link to nothing is only a leftover of matching, or a
        // refusal of one episode, and links the anime to nothing.
        if (!Replace && (crossReferences.GetSeriesLinks(AnimeID, source).Count > 0 || crossReferences.GetMovieLinksForSeries(AnimeID, source).Count > 0 ||
            crossReferences.GetEpisodeLinksForSeries(AnimeID, source).Any(link => link.ProviderID is not null)))
        {
            _logger.LogDebug("Not searching {Source} for anime {AnimeID}: it is already linked.", source, AnimeID);
            return;
        }

        var token = cancellationAccessor.Token;
        var candidates = await provider.FindAutoLinks(AnimeID, token).ConfigureAwait(false);
        _logger.LogDebug("{Provider} found {Count} candidates on {Source} for anime {AnimeID}.", info.Name, candidates.Count, source, AnimeID);
        var linked = await linkingService.ApplyAutoLinks(source, AnimeID, candidates, replace: Replace, token).ConfigureAwait(false);
        if (linked.Count is 0)
            return;

        // Writing a link queues nothing by itself, so the search refreshes what it linked;
        // what is already fresh is skipped unless forced.
        await providerScheduler.ScheduleRefresh(info, AnimeID, force: Force || Replace, options: MetadataProviderScheduler.FullRefresh(MetadataRefreshReason.Linked), cancellationToken: token)
            .ConfigureAwait(false);
    }

    #endregion
}
