using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Purge the stored series and films of every metadata source, TMDB
///   included, that nothing links to any more, and the collections none of
///   whose members anything links to.
/// </summary>
/// <remarks>
///   Only queues the purges, which run on their own; the progress covers
///   the queuing. <see cref="PurgeUnusedMetadataAction"/> does it for one
///   source.
/// </remarks>
/// <param name="providerManager">Lists the metadata providers.</param>
/// <param name="purgeService">Queues the purges.</param>
public sealed class PurgeAllUnusedMetadataAction(IMetadataProviderManager providerManager, IMetadataPurgeService purgeService) : IScheduledAction
{
    public string Name => "Purge Unused Metadata";

    public string? Description
        => "Removes the stored series, films and collections of every metadata source, TMDB included, that are not linked to any AniDB anime.";

    public ActionCategory Category => ActionCategory.Destructive;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to remove all unlinked series, films and collections of every metadata source, TMDB included?";

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => MetadataPurges.ForEach(
            MetadataPurges.StoredSources(providerManager),
            (source, stage, ct) => purgeService.PurgeUnused(source, null, null, stage, ct),
            progress,
            token
        );
}
