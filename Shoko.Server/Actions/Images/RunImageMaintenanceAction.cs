using System;
using System.Collections.Generic;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Image;

namespace Shoko.Server.Actions;

/// <summary>
///   Purge the orphaned images and validate the rest, as the image settings say.
/// </summary>
/// <param name="scheduler">The queue.</param>
public sealed class RunImageMaintenanceAction(IQueueScheduler scheduler) : QueueJobScheduledAction<PeriodicImageMaintenanceJob>(scheduler)
{
    public override string Name => "Run Image Maintenance";

    public override string? Description => "Purges the images nothing has used for a week and validates the rest, each when the image settings turn it on.";

    public override ActionCategory Category => ActionCategory.Images;

    public override IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.Every(TimeSpan.FromHours(24))];
}
