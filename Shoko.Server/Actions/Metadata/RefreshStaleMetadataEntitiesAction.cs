using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Refresh every stub and stale creator, character, studio and network
///   something names, from the provider taking its source and kind.
/// </summary>
/// <remarks>
///   Only queues the refreshes, one job per entry, which run on their own;
///   the progress covers the queuing. A source with no such provider keeps
///   its stubs.
/// </remarks>
/// <param name="entityScheduler">Finds the due entries and queues their refreshes.</param>
public sealed class RefreshStaleMetadataEntitiesAction(MetadataEntityRefreshScheduler entityScheduler) : IScheduledAction
{
    public string Name => "Refresh Stale People, Studios and Networks";

    public string? Description
        => "Has the metadata providers that refresh people, studios and networks one at a time refresh each one still a stub, "
            + "or older than the provider allows, that a series or film names.";

    public ActionCategory Category => ActionCategory.Maintenance;

    public IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.Every(TimeSpan.FromHours(24))];

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => entityScheduler.ScheduleAllDue(progress, token);
}
