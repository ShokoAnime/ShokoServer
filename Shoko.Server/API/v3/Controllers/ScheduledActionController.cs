using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.ScheduledActions.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Action;
using Shoko.Server.Scheduling;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// The scheduled actions: work that runs on its own on triggers the admin
/// sets, and that an admin can run by hand. Admins only.
/// </summary>
/// <param name="scheduleService">The scheduled actions.</param>
/// <param name="settingsProvider">The settings.</param>
[ApiController]
[Route("/api/v{version:apiVersion}/Action/Scheduled"), Tags("Action")]
[ApiV3]
[Authorize("admin")]
public class ScheduledActionController(IScheduledActionService scheduleService, ISettingsProvider settingsProvider) : BaseController(settingsProvider)
{
    #region Constants

    private const string NotFoundMessage = "No scheduled action has the ID.";

    #endregion

    #region Scheduled Actions

    /// <summary>
    /// List the scheduled actions with their triggers, when they last ran and
    /// run next, and where their current run is in the queue.
    /// </summary>
    /// <returns>The scheduled actions.</returns>
    [HttpGet]
    public ActionResult<List<ScheduledAction>> GetScheduledActions()
        => scheduleService.GetScheduledActions().Select(ScheduledAction.FromScheduledActionInfo).ToList();

    /// <summary>
    /// Get one scheduled action.
    /// </summary>
    /// <param name="actionID">Scheduled action ID.</param>
    /// <response code="404">No scheduled action has the ID.</response>
    /// <returns>The scheduled action.</returns>
    [HttpGet("{actionID:guid}")]
    public ActionResult<ScheduledAction> GetScheduledAction([FromRoute] Guid actionID)
        => scheduleService.GetScheduledAction(actionID) is { } info
            ? ScheduledAction.FromScheduledActionInfo(info)
            : NotFound(NotFoundMessage);

    #endregion

    #region Triggers

    /// <summary>
    /// Get the triggers in effect for a scheduled action: the admin's, or its
    /// defaults when the admin set none.
    /// </summary>
    /// <param name="actionID">Scheduled action ID.</param>
    /// <response code="404">No scheduled action has the ID.</response>
    /// <returns>The triggers.</returns>
    [HttpGet("{actionID:guid}/Triggers")]
    public ActionResult<List<ScheduledActionTrigger>> GetTriggers([FromRoute] Guid actionID)
        => scheduleService.GetScheduledAction(actionID) is { } info
            ? info.Triggers.Select(ScheduledActionTrigger.FromTrigger).ToList()
            : NotFound(NotFoundMessage);

    /// <summary>
    /// Replace the triggers of a scheduled action. An empty list means it
    /// never runs on its own.
    /// </summary>
    /// <param name="actionID">Scheduled action ID.</param>
    /// <param name="body">
    /// The triggers. Each type takes only its own fields: <c>Interval</c> takes
    /// <c>Interval</c>, <c>Daily</c> takes <c>TimeOfDay</c>, <c>Weekly</c> takes
    /// <c>DaysOfWeek</c> and <c>TimeOfDay</c>, <c>Monthly</c> takes
    /// <c>DaysOfMonth</c> and <c>TimeOfDay</c>, and <c>Startup</c> takes none.
    /// A time of day is whole minutes in the server's time zone; an interval is
    /// whole minutes, no shorter than the scheduled action's minimum.
    /// </param>
    /// <response code="400">
    /// A trigger is invalid, its interval is under the minimum, or the daily,
    /// weekly and monthly triggers run closer together than it.
    /// </response>
    /// <response code="404">No scheduled action has the ID.</response>
    /// <returns>The scheduled action with its new triggers.</returns>
    [HttpPut("{actionID:guid}/Triggers")]
    public ActionResult<ScheduledAction> SetTriggers(
        [FromRoute] Guid actionID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] List<ScheduledActionTrigger> body
    ) => SetTriggersCore(actionID, _ => body.Select(trigger => trigger.ToTrigger()).ToList());

    /// <summary>
    /// Add a trigger to the ones in effect for a scheduled action, which makes
    /// them the admin's own if they were the defaults.
    /// </summary>
    /// <param name="actionID">Scheduled action ID.</param>
    /// <param name="body">The trigger.</param>
    /// <response code="400">
    /// The trigger is invalid, its interval is under the minimum, or the daily,
    /// weekly and monthly triggers run closer together than it.
    /// </response>
    /// <response code="404">No scheduled action has the ID.</response>
    /// <returns>The scheduled action with its new triggers.</returns>
    [HttpPost("{actionID:guid}/Triggers")]
    public ActionResult<ScheduledAction> AddTrigger(
        [FromRoute] Guid actionID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ScheduledActionTrigger body
    ) => SetTriggersCore(actionID, current => [.. current, body.ToTrigger()]);

    /// <summary>
    /// Put the scheduled action's own default triggers back in effect.
    /// </summary>
    /// <param name="actionID">Scheduled action ID.</param>
    /// <response code="404">No scheduled action has the ID.</response>
    /// <returns>The scheduled action with its default triggers.</returns>
    [HttpDelete("{actionID:guid}/Triggers")]
    public ActionResult<ScheduledAction> ResetTriggers([FromRoute] Guid actionID)
        => SetTriggersCore(actionID, _ => null);

    /// <summary>
    /// Remove a trigger from the ones in effect for a scheduled action, by its
    /// position, which makes them the admin's own if they were the defaults.
    /// </summary>
    /// <param name="actionID">Scheduled action ID.</param>
    /// <param name="index">The position of the trigger in the list, from 0.</param>
    /// <response code="404">No scheduled action has the ID, or it has no trigger at the position.</response>
    /// <returns>The scheduled action with its new triggers.</returns>
    [HttpDelete("{actionID:guid}/Triggers/{index:int}")]
    public ActionResult<ScheduledAction> RemoveTrigger([FromRoute] Guid actionID, [FromRoute] int index)
    {
        if (scheduleService.GetScheduledAction(actionID) is not { } info)
            return NotFound(NotFoundMessage);

        if (index < 0 || index >= info.Triggers.Count)
            return NotFound("The scheduled action has no trigger at the position.");

        return SetTriggersCore(actionID, current => current.Where((_, position) => position != index).ToList());
    }

    /// <summary>
    /// The shared half of the trigger endpoints: work out the new triggers
    /// from the ones in effect, check each, and store them.
    /// </summary>
    /// <param name="actionID">Scheduled action ID.</param>
    /// <param name="change">
    /// Makes the new triggers from the ones in effect, or returns
    /// <see langword="null"/> to go back to the defaults.
    /// </param>
    /// <returns>The scheduled action, a validation problem, or not found.</returns>
    private ActionResult<ScheduledAction> SetTriggersCore(Guid actionID, Func<IReadOnlyList<ActionTrigger>, IReadOnlyList<ActionTrigger>?> change)
    {
        if (scheduleService.GetScheduledAction(actionID) is not { } info)
            return NotFound(NotFoundMessage);

        var triggers = change(info.Triggers);
        if (triggers is null)
            return ScheduledAction.FromScheduledActionInfo(scheduleService.ResetTriggers(actionID));

        var errors = new Dictionary<string, IReadOnlyList<string>>();
        for (var index = 0; index < triggers.Count; index++)
        {
            if (triggers[index].GetValidationError(info.MinimumInterval) is { } error)
                errors[string.Create(CultureInfo.InvariantCulture, $"[{index}]")] = [error];
        }

        if (errors.Count is 0 && ActionTriggerSchedule.GetSpacingError(triggers, info.MinimumInterval) is { } spacingError)
            errors[string.Empty] = [spacingError];

        if (errors.Count > 0)
            return ValidationProblem(errors);

        return ScheduledAction.FromScheduledActionInfo(scheduleService.SetTriggers(actionID, triggers));
    }

    #endregion

    #region Runs

    /// <summary>
    /// Queue a run of the scheduled action now. It is not held back by the
    /// minimum interval, but a run still waiting or running is not queued
    /// twice. It counts for the schedule only when the scheduled action says
    /// runs by hand do.
    /// </summary>
    /// <param name="actionID">Scheduled action ID.</param>
    /// <param name="token">Cancellation token.</param>
    /// <response code="400">The scheduled action refused to run.</response>
    /// <response code="404">No scheduled action has the ID.</response>
    /// <returns>The scheduled action afterwards.</returns>
    [HttpPost("{actionID:guid}")]
    public async Task<ActionResult<ScheduledAction>> Invoke([FromRoute] Guid actionID, CancellationToken token)
    {
        if (scheduleService.GetScheduledAction(actionID) is null)
            return NotFound(NotFoundMessage);

        if (await scheduleService.InvokeAsync(actionID, token) is { } refusal)
            return BadRequest(refusal.Reason);

        return ScheduledAction.FromScheduledActionInfo(scheduleService.GetScheduledAction(actionID)!);
    }

    /// <summary>
    /// Cancel the current run of the scheduled action. A waiting run is taken
    /// out of the queue. A running one whose job observes cancellation is
    /// asked to stop and shows as cancellation requested until it does, which
    /// cannot be undone; one whose job does not stays running.
    /// </summary>
    /// <param name="actionID">Scheduled action ID.</param>
    /// <response code="404">No scheduled action has the ID.</response>
    /// <returns>The scheduled action afterwards.</returns>
    [HttpPost("{actionID:guid}/Cancel")]
    public async Task<ActionResult<ScheduledAction>> Cancel([FromRoute] Guid actionID)
    {
        if (scheduleService.GetScheduledAction(actionID) is null)
            return NotFound(NotFoundMessage);

        return ScheduledAction.FromScheduledActionInfo(await scheduleService.Cancel(actionID));
    }

    #endregion
}
