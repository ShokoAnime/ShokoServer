using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;
using Shoko.Server.Services;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/Group/{groupID:int}/Action")]
[ApiV3]
[Authorize]
public class GroupActionController(ActionService actionService, AnimeGroupRepository groups, ISettingsProvider settingsProvider) : BaseController(settingsProvider)
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
    /// <param name="token">Cancellation token.</param>
    [HttpPost("{actionID:guid}")]
    public async Task<ActionResult> Invoke(
        [FromRoute, Range(1, int.MaxValue)] int groupID,
        [FromRoute] Guid actionID,
        CancellationToken token
    )
    {
        if (actionService.GetActionInfo(actionID) is null)
            return NotFound("Action not found.");

        var groupEntity = groups.GetByID(groupID);
        if (groupEntity is null)
            return NotFound("Group not found.");

        var validation = await actionService.InvokeAsync(actionID, groupEntity, User, token);
        return validation is null ? Ok() : BadRequest(validation.Reason);
    }
}
