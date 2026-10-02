using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Purge every stored collection of every metadata source, TMDB included.
/// </summary>
/// <remarks>
///   Nothing links a collection, so no link is lost: a refresh of a series or
///   film a collection holds stores it again. Only queues the purges, which
///   run on their own; the progress covers the queuing.
///   <see cref="PurgeMetadataCollectionsAction"/> does it for one source.
/// </remarks>
/// <param name="providerManager">Lists the metadata providers.</param>
/// <param name="purgeService">Queues the purges.</param>
public sealed class PurgeAllMetadataCollectionsAction(IMetadataProviderManager providerManager, IMetadataPurgeService purgeService) : IScheduledAction
{
    public string Name => "Purge Metadata Collections";

    public string? Description
        => "Removes every stored collection of every metadata source, TMDB included. A refresh of a series or film a collection holds stores "
            + "it again.";

    public ActionCategory Category => ActionCategory.Destructive;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to remove every collection of every metadata source, TMDB included?";

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => MetadataPurges.ForEach(
            MetadataPurges.StoredSources(providerManager),
            (source, stage, ct) => purgeService.PurgeCollections(source, stage, ct),
            progress,
            token
        );
}
