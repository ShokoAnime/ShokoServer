using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.UI;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Action;
using Shoko.Server.Services;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]"), Tags("Action")]
[ApiV3]
[Authorize]
public class ActionController(ActionService actionService, ISettingsProvider settingsProvider) : BaseController(settingsProvider)
{
    /// <summary>
    ///   List all registered actions. <paramref name="scope"/> is an optional
    ///   filter — omitting it lists everything.
    /// </summary>
    /// <param name="scope">Optional. Filter to actions of a specific scope.</param>
    [HttpGet]
    public ActionResult<IEnumerable<ActionInfo>> GetActions([FromQuery] ActionScope? scope)
        => Ok(actionService.GetActions(scope, User.IsAdmin == 1 ? null : ActionPermission.User)
            .Select(ActionInfo.FromExecutableActionInfo));

    /// <summary>
    ///   Get a render-ready UI definition for the parameters of the action with
    ///   the given ID, in the same shape a configuration editor is described by.
    /// </summary>
    /// <remarks>
    ///   One endpoint covers every scope: an action's parameters come off the
    ///   action type, which does not vary by the entity it is invoked against,
    ///   so a series-scoped action is described here the same as a global one.
    ///   Ask only when the listing said <see cref="ActionInfo.HasParameters"/>.
    /// </remarks>
    /// <param name="actionID">Action ID.</param>
    /// <returns>The UI definition for the action's parameters.</returns>
    [HttpGet("{actionID:guid}/UiDefinition")]
    public ActionResult<UiDefinition> GetActionUiDefinition([FromRoute] Guid actionID)
    {
        if (actionService.GetActionInfo(actionID) is not { } info)
            return NotFound("Action not found.");

        if (info.Parameters is not { } parameters)
            return NotFound("Action does not take any parameters.");

        return parameters;
    }

    /// <summary>
    ///   Invoke a global action by its ID. Returns 200 (accepted), or 400 with
    ///   a reason when the action's validation (or the caller's permission)
    ///   rejects the invocation.
    /// </summary>
    /// <param name="actionID">Action ID.</param>
    /// <param name="parameters">
    ///   Optional. The action's invocation parameters. Omit the body entirely
    ///   for an action that takes none.
    /// </param>
    /// <param name="token">Cancellation token.</param>
    [HttpPost("{actionID:guid}")]
    public async Task<ActionResult> Invoke(
        [FromRoute] Guid actionID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JObject? parameters,
        CancellationToken token
    )
    {
        if (actionService.GetActionInfo(actionID) is null)
            return NotFound("Action not found.");

        if (actionService.ValidateParameters(actionID, parameters) is { Count: > 0 } errors)
            return ValidationProblem(errors);

        // No body takes the same overload it always has, so an action that
        // declares no parameters is invoked exactly as before.
        var parameterMap = parameters.ToParameters();
        var validation = parameterMap is null
            ? await actionService.InvokeAsync(actionID, User, token)
            : await actionService.InvokeAsync(actionID, parameterMap, User, token);
        return validation is null ? Ok() : BadRequest(validation.Reason);
    }
}
