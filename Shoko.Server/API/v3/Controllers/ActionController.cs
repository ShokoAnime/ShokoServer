using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Actions.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Action;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]"), Tags("Action")]
[ApiV3]
[Authorize]
public class ActionController(IActionService actionService, ISettingsProvider settingsProvider) : BaseController(settingsProvider)
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
    ///   Invoke a global action by its ID. Returns 200 (accepted), or 400 with
    ///   a reason when the action's validation (or the caller's permission)
    ///   rejects the invocation.
    /// </summary>
    /// <param name="actionID">Action ID.</param>
    /// <param name="token">Cancellation token.</param>
    [HttpPost("{actionID:guid}")]
    public async Task<ActionResult> Invoke([FromRoute] Guid actionID, CancellationToken token)
    {
        if (actionService.GetActionInfo(actionID) is null)
            return NotFound("Action not found.");

        var validation = await actionService.InvokeAsync(actionID, User, token);
        return validation is null ? Ok() : BadRequest(validation.Reason);
    }
}
