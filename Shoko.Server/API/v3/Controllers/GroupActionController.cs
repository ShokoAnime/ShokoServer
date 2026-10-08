using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Actions.Services;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.UI;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Action;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/Group/{groupID:int}/Action"), Tags("Action")]
[ApiV3]
[Authorize]
public class GroupActionController(IActionService actionService, AnimeGroupRepository groups, ISettingsProvider settingsProvider) : BaseController(settingsProvider)
{
    /// <summary>
    ///   Invoke a group-scoped action by its ID. Entity existence is
    ///   validated before anything is enqueued; returns 404 if the group
    ///   isn't found, 200 (accepted) on success, or 400 with a reason when
    ///   the action's validation (or the caller's permission) rejects the
    ///   invocation.
    /// </summary>
    /// <param name="groupID">Group ID.</param>
    /// <param name="actionID">Action ID.</param>
    /// <param name="parameters">
    ///   Optional. The action's invocation parameters. Omit the body entirely
    ///   for an action that takes none.
    /// </param>
    /// <param name="token">Cancellation token.</param>
    [HttpPost("{actionID:guid}")]
    public async Task<ActionResult> Invoke(
        [FromRoute, Range(1, int.MaxValue)] int groupID,
        [FromRoute] Guid actionID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JObject? parameters,
        CancellationToken token
    )
    {
        if (actionService.GetActionInfo(actionID) is null)
            return NotFound("Action not found.");

        var groupEntity = groups.GetByID(groupID);
        if (groupEntity is null)
            return NotFound("Group not found.");

        if (actionService.ValidateParameters(actionID, parameters) is { Count: > 0 } errors)
            return ValidationProblem(errors);

        // Parameters are an argument like any other now, and null is what an
        // action taking none has always been invoked with.
        var validation = await actionService.InvokeAsync(actionID, groupEntity, parameters.ToParameters(), caller: User, token: token);
        return validation is null ? Ok() : BadRequest(validation.Reason);
    }

    /// <summary>
    ///   List the options the server offers for one of a group-scoped
    ///   action's parameters, as the parameter's <c>OptionsRoute</c> says to.
    /// </summary>
    /// <param name="groupID">Group ID.</param>
    /// <param name="actionID">Action ID.</param>
    /// <param name="parameters">Optional. The parameters entered so far.</param>
    /// <param name="path">Path to the parameter, the same path a configuration's custom action is invoked with.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>The options, in the order the provider listed them.</returns>
    [HttpPost("{actionID:guid}/Options")]
    public async Task<ActionResult<IReadOnlyList<UiOption>>> GetOptions(
        [FromRoute, Range(1, int.MaxValue)] int groupID,
        [FromRoute] Guid actionID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JObject? parameters,
        [FromQuery] string path = "",
        CancellationToken token = default
    )
    {
        if (actionService.GetActionInfo(actionID) is null)
            return NotFound("Action not found.");

        var groupEntity = groups.GetByID(groupID);
        if (groupEntity is null)
            return NotFound("Group not found.");

        if (actionService.ValidateParameters(actionID, parameters) is { Count: > 0 } errors)
            return ValidationProblem(errors);

        try
        {
            return Ok(await actionService.GetParameterOptionsAsync(actionID, groupEntity, path, parameters.ToParameters(), User, token));
        }
        catch (GenericValidationException ex)
        {
            return ValidationProblem(ex.ValidationErrors);
        }
    }
}
