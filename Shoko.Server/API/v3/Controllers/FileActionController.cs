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
[Route("/api/v{version:apiVersion}/File/{fileID:int}/Action")]
[ApiV3]
[Authorize]
public class FileActionController(ActionService actionService, VideoLocalRepository videos, ISettingsProvider settingsProvider) : BaseController(settingsProvider)
{
    /// <summary>
    ///   Invoke a video-scoped action by its ID. Entity existence is
    ///   validated before anything is enqueued; returns 404 if the file
    ///   isn't found, 200 (accepted) on success, or 400 with a reason when
    ///   the action's validation (or the caller's permission) rejects the
    ///   invocation.
    /// </summary>
    /// <param name="fileID">File ID.</param>
    /// <param name="actionID">Action ID.</param>
    /// <param name="token">Cancellation token.</param>
    [HttpPost("{actionID:guid}")]
    public async Task<ActionResult> Invoke(
        [FromRoute, Range(1, int.MaxValue)] int fileID,
        [FromRoute] Guid actionID,
        CancellationToken token
    )
    {
        if (actionService.GetActionInfo(actionID) is null)
            return NotFound("Action not found.");

        var videoEntity = videos.GetByID(fileID);
        if (videoEntity is null)
            return NotFound("File not found.");

        var validation = await actionService.InvokeAsync(actionID, videoEntity, User, token);
        return validation is null ? Ok() : BadRequest(validation.Reason);
    }
}
