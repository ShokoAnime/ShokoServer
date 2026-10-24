using System;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.API.v3.Models.Action;

namespace Shoko.Server.API.SignalR.Models;

/// <summary>
/// Dispatched when a scheduled action's run starts or ends.
/// </summary>
public class ScheduledActionRunSignalRModel
{
    /// <summary>
    /// Builds the model of a run that started.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    public ScheduledActionRunSignalRModel(ScheduledActionRunStartedEventArgs args)
    {
        Action = ScheduledAction.FromScheduledActionInfo(args.Action);
        StartedAt = args.StartedAt;
    }

    /// <summary>
    /// Builds the model of a run that ended.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    public ScheduledActionRunSignalRModel(ScheduledActionRunCompletedEventArgs args)
    {
        Action = ScheduledAction.FromScheduledActionInfo(args.Action);
        StartedAt = args.StartedAt;
        CompletedAt = args.CompletedAt;
        IsSuccess = args.Exception is null;
        IsCancelled = args.IsCancelled;
        Error = args.Exception is { } exception && !args.IsCancelled ? exception.Message : null;
    }

    /// <summary>
    /// The scheduled action, as it is now.
    /// </summary>
    public ScheduledAction Action { get; }

    /// <summary>
    /// When the run started, in UTC.
    /// </summary>
    public DateTime StartedAt { get; }

    /// <summary>
    /// When the run ended, in UTC, or <c>null</c> while it runs.
    /// </summary>
    public DateTime? CompletedAt { get; }

    /// <summary>
    /// Whether the run ended well, or <c>null</c> while it runs.
    /// </summary>
    public bool? IsSuccess { get; }

    /// <summary>
    /// Whether the run was cancelled, or <c>null</c> while it runs.
    /// </summary>
    public bool? IsCancelled { get; }

    /// <summary>
    /// Why the run failed, or <c>null</c> when it did not.
    /// </summary>
    public string? Error { get; }
}
