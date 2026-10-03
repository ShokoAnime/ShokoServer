using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Remove the alternate orderings every metadata source keeps.
/// </summary>
/// <remarks>
///   The users' own orderings are kept. A refresh stores a source's
///   orderings again. <see cref="PurgeMetadataOrderingsAction"/> does it for
///   one source.
/// </remarks>
/// <param name="orderingService">Keeps the orderings.</param>
public sealed class PurgeAllMetadataOrderingsAction(MetadataOrderingService orderingService) : IScheduledAction
{
    public string Name => "Purge Metadata Alternate Orderings";

    public string? Description
        => "Removes the alternate orderings of every metadata source. The users' own orderings are kept.";

    public ActionCategory Category => ActionCategory.Destructive;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to remove the alternate orderings of every metadata source?";

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => MetadataPurges.ForEach(
            orderingService.GetGlobalOrderingSources(),
            (source, stage, ct) => Task.FromResult(orderingService.RemoveOrderings(source, stage, ct)),
            progress,
            token
        );
}
