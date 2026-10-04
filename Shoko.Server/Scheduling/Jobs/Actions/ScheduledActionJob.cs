using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Services;

#nullable enable
using Shoko.QueueProcessor.Abstractions;
namespace Shoko.Server.Scheduling.Jobs.Actions;

/// <summary>
/// Runs one scheduled action, with the job's progress and cancellation, and
/// reports 100 once the action returns. Its key is the scheduled action's ID
/// alone, so a run is never queued twice. It is long running, so the watchdog
/// leaves every scheduled action alone.
/// </summary>
/// <param name="services">The job's own container, which the action is resolved from.</param>
/// <param name="registry">The scheduled actions.</param>
/// <param name="cancellation">The job's cancellation, which also makes it cancellable.</param>
/// <param name="progress">The job's progress.</param>
[LongRunning]
[DatabaseRequired]
[JobKeyMember("ScheduledAction")]
[JobKeyGroup(JobKeyGroup.Actions)]
[JobPriority(Default = 100, Prioritized = 150)]
public class ScheduledActionJob(
    IServiceProvider services,
    ScheduledActionRegistry registry,
    IJobCancellationAccessor cancellation,
    IJobProgressAccessor progress
) : BaseJob
{
    #region Properties

    /// <summary>
    /// The ID of the scheduled action to run.
    /// </summary>
    [JobKeyMember(index: 0)]
    public Guid ActionId { get; set; }

    /// <inheritdoc/>
    public override string TypeName => "Scheduled Action";

    /// <inheritdoc/>
    public override string Title => registry.GetAction(ActionId)?.Name ?? ActionId.ToString();

    /// <inheritdoc/>
    public override Dictionary<string, object> Details => new()
    {
        { "Action", Title },
    };

    #endregion

    #region Execute

    /// <inheritdoc/>
    public override async Task Execute()
    {
        if (registry.GetAction(ActionId) is not { } definition)
        {
            _logger.LogWarning("Skipping scheduled action {ActionId}: no scheduled action has the ID", ActionId);
            return;
        }

        // From the job's own container, so the action's scoped dependencies
        // are this run's and not shared by every run.
        var action = registry.CreateInstance(ActionId, services);

        // Asked again here, as the answer given before queuing can be hours old.
        if (await action.Validate(cancellation.Token) is { } refusal)
        {
            _logger.LogWarning("Skipping scheduled action \"{ActionName}\" ({ActionId}): {Reason}", definition.Name, ActionId, refusal.Reason);
            return;
        }

        _logger.LogInformation("Running scheduled action \"{ActionName}\" ({ActionId})", definition.Name, ActionId);
        await action.Execute(progress.Progress, cancellation.Token);

        // A run that ended well is done, whether the action reported it or not.
        progress.Progress.Report(100);
        _logger.LogInformation("Finished scheduled action \"{ActionName}\" ({ActionId})", definition.Name, ActionId);
    }

    #endregion
}
