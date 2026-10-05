using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Fetch what stored entries name but the stores lack or hold stale: every
///   stub and stale creator, character, studio and network, and every
///   collection a linked film names that is not stored.
/// </summary>
/// <remarks>
///   Only for the kinds a provider of the source has turned on, through the
///   existing refresh jobs, one per entry, merging with any already queued;
///   the progress covers the queuing. A source with no such provider keeps
///   its stubs and leaves its collections unfetched.
/// </remarks>
/// <param name="entityScheduler">Finds the due people, studios and networks and queues their refreshes.</param>
/// <param name="collectionScheduler">Finds the missing collections and queues their fetches.</param>
public sealed class RefreshStaleMetadataEntitiesAction(
    MetadataEntityRefreshScheduler entityScheduler,
    MetadataCollectionRefreshScheduler collectionScheduler
) : IScheduledAction
{
    public string Name => "Refresh Missing and Stale People, Studios, Networks and Collections";

    public string? Description
        => "Has the metadata providers refresh each person, studio and network a series or film names that is still a stub or older than "
            + "the provider allows, and fetch each collection a linked film names that is not stored, for the kinds they are turned on for.";

    public ActionCategory Category => ActionCategory.Maintenance;

    public IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.AtStartup, ActionTrigger.Every(TimeSpan.FromHours(24))];

    public async Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        var stages = new StagedProgress(progress, 2);
        stages.Report(0);
        await entityScheduler.ScheduleAllDue(stages, token).ConfigureAwait(false);
        stages.NextStage();
        await collectionScheduler.ScheduleAllMissing(stages, token).ConfigureAwait(false);
        stages.Complete();
    }
}
