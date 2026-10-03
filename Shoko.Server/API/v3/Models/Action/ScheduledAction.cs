using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;

#nullable enable
namespace Shoko.Server.API.v3.Models.Action;

/// <summary>
/// A scheduled action with its triggers, when it last ran and runs next, and
/// where its current run is in the queue.
/// </summary>
public class ScheduledAction
{
    /// <summary>
    /// The scheduled action's stable UUIDv5 identifier.
    /// </summary>
    [Required]
    public Guid ID { get; set; }

    /// <summary>
    /// The display name.
    /// </summary>
    [Required]
    public required string Name { get; set; }

    /// <summary>
    /// The description, if any.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The category it is listed under.
    /// </summary>
    [Required]
    public ActionCategory Category { get; set; }

    /// <summary>
    /// The display name of the category: the owning plugin's name for
    /// <see cref="ActionCategory.PluginInferred"/>, otherwise the category's
    /// own name.
    /// </summary>
    [Required]
    public required string CategoryName { get; set; }

    /// <summary>
    /// Whether to ask before running it by hand.
    /// </summary>
    [Required]
    public bool RequiresConfirmation { get; set; }

    /// <summary>
    /// The question to ask before running it by hand, or
    /// <see langword="null"/> for a generic one.
    /// </summary>
    public string? ConfirmationMessage { get; set; }

    /// <summary>
    /// The triggers in effect: the admin's, or the action's defaults when the
    /// admin set none. Empty when the action never runs on its own.
    /// </summary>
    [Required]
    public required List<ScheduledActionTrigger> Triggers { get; set; }

    /// <summary>
    /// The triggers the action declares for itself.
    /// </summary>
    [Required]
    public required List<ScheduledActionTrigger> DefaultTriggers { get; set; }

    /// <summary>
    /// The shortest time after a run before the action runs on its own again,
    /// in whole minutes, from the start of the minute of <see cref="LastScheduledRunAt"/>
    /// (or <see cref="LastRunAt"/> when <see cref="ScheduleCountsManualRuns"/> is set).
    /// Triggers closer together are refused and no mix of them runs the action
    /// sooner; a run by hand is not held back.
    /// </summary>
    [Required]
    public TimeSpan MinimumInterval { get; set; }

    /// <summary>
    /// Whether a run by hand counts for the schedule like a run a trigger
    /// queued, so the triggers count from <see cref="LastRunAt"/> rather than
    /// from <see cref="LastScheduledRunAt"/>. Set by the action, for one whose
    /// service minds how often it is asked.
    /// </summary>
    [Required]
    public bool ScheduleCountsManualRuns { get; set; }

    /// <summary>
    /// Whether the admin set the triggers, rather than the defaults being in
    /// effect.
    /// </summary>
    [Required]
    public bool HasCustomTriggers { get; set; }

    /// <summary>
    /// When the action was last queued, by a trigger or by hand, or its run's
    /// queue job last started, whichever is later, or <see langword="null"/>
    /// when it never was.
    /// </summary>
    public DateTime? LastRunAt { get; set; }

    /// <summary>
    /// When a trigger last queued the action, or the queue job of a run a
    /// trigger queued last started, whichever is later, or
    /// <see langword="null"/> when no trigger ever did.
    /// </summary>
    public DateTime? LastScheduledRunAt { get; set; }

    /// <summary>
    /// When a trigger queues the action next, never sooner than
    /// <see cref="MinimumInterval"/> after the last run the schedule counts,
    /// or <see langword="null"/> when only start-up and queue-cleared triggers, or
    /// none, would.
    /// It lies in the past for a run that is due and about to be queued. A
    /// daily, weekly or monthly run is on its wall-clock minute, at 0 seconds.
    /// </summary>
    public DateTime? NextRunAt { get; set; }

    /// <summary>
    /// Where the queue job of its run is.
    /// </summary>
    [Required]
    public ScheduledActionState State { get; set; }

    /// <summary>
    /// How far the running job is, as a percentage from 0 to 100, or
    /// <see langword="null"/> when it is not running or does not report.
    /// </summary>
    public decimal? Progress { get; set; }

    /// <summary>
    /// Whether the run can be cancelled now: a waiting one always can, and a
    /// running one when its job observes cancellation.
    /// </summary>
    [Required]
    public bool IsCancellable { get; set; }

    /// <summary>
    /// The queue key of the job a run uses, as the queue endpoints show it.
    /// </summary>
    [Required]
    public required string JobKey { get; set; }

    /// <summary>
    /// Builds the API form of a scheduled action.
    /// </summary>
    /// <param name="info">The scheduled action.</param>
    /// <returns>The API form.</returns>
    public static ScheduledAction FromScheduledActionInfo(ScheduledActionInfo info) => new()
    {
        ID = info.ID,
        Name = info.Name,
        Description = info.Description,
        Category = info.Category,
        CategoryName = info.CategoryName,
        RequiresConfirmation = info.RequiresConfirmation,
        ConfirmationMessage = info.ConfirmationMessage,
        Triggers = info.Triggers.Select(ScheduledActionTrigger.FromTrigger).ToList(),
        DefaultTriggers = info.DefaultTriggers.Select(ScheduledActionTrigger.FromTrigger).ToList(),
        MinimumInterval = info.MinimumInterval,
        ScheduleCountsManualRuns = info.ScheduleCountsManualRuns,
        HasCustomTriggers = info.HasCustomTriggers,
        LastRunAt = info.LastRunAt,
        LastScheduledRunAt = info.LastScheduledRunAt,
        NextRunAt = info.NextRunAt,
        State = info.State,
        Progress = info.Progress,
        IsCancellable = info.IsCancellable,
        JobKey = info.JobKey,
    };
}
