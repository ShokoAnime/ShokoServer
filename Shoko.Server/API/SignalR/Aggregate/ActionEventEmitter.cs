using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.ScheduledActions.Services;
using Shoko.Abstractions.User;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.API.v3.Models.Action;

namespace Shoko.Server.API.SignalR.Aggregate;

/// <summary>
/// The <c>action</c> feed, for admins only: the scheduled actions' triggers
/// being set or reset, as <c>action:triggers.changed</c> with the action as it
/// is now, their runs starting and ending, as <c>action:run.started</c> and
/// <c>action:run.completed</c>, and actions coming, going or changing while the
/// server runs, as <c>action:actions.changed</c>.
/// </summary>
public class ActionEventEmitter : BaseEventEmitter, IDisposable
{
    public override string Name => "action";

    private readonly IScheduledActionService _scheduledActions;

    private readonly ILogger<ActionEventEmitter> _logger;

    public ActionEventEmitter(IHubContext<AggregateHub> hub, IScheduledActionService scheduledActions, ILogger<ActionEventEmitter> logger) : base(hub)
    {
        _scheduledActions = scheduledActions;
        _logger = logger;
        _scheduledActions.TriggersChanged += OnTriggersChanged;
        _scheduledActions.RunStarted += OnRunStarted;
        _scheduledActions.RunCompleted += OnRunCompleted;
        _scheduledActions.ActionsChanged += OnActionsChanged;
    }

    public void Dispose()
    {
        _scheduledActions.TriggersChanged -= OnTriggersChanged;
        _scheduledActions.RunStarted -= OnRunStarted;
        _scheduledActions.RunCompleted -= OnRunCompleted;
        _scheduledActions.ActionsChanged -= OnActionsChanged;
    }

    /// <inheritdoc/>
    protected override bool CanConnect(IUser user)
        => user.IsAdmin;

    private async void OnTriggersChanged(object? sender, ScheduledActionTriggersChangedEventArgs e)
    {
        try
        {
            await SendAsync("triggers.changed", ScheduledAction.FromScheduledActionInfo(e.Current));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'triggers.changed' event.");
        }
    }

    private async void OnActionsChanged(object? sender, ScheduledActionsChangedEventArgs e)
    {
        try
        {
            await SendAsync("actions.changed", new ScheduledActionsChangedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'actions.changed' event.");
        }
    }

    private async void OnRunStarted(object? sender, ScheduledActionRunStartedEventArgs e)
    {
        try
        {
            await SendAsync("run.started", new ScheduledActionRunSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'run.started' event.");
        }
    }

    private async void OnRunCompleted(object? sender, ScheduledActionRunCompletedEventArgs e)
    {
        try
        {
            await SendAsync("run.completed", new ScheduledActionRunSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'run.completed' event.");
        }
    }
}
