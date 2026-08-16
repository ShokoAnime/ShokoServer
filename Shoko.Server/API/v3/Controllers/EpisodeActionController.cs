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
[Route("/api/v{version:apiVersion}/Episode/{episodeId:int}/Action")]
[ApiV3]
[Authorize]
public class EpisodeActionController(ActionService actionService, AnimeEpisodeRepository episodes, ISettingsProvider settingsProvider) : BaseController(settingsProvider)
{
    /// <summary>
    ///   Invoke an episode-scoped action by its ID. Entity existence is
    ///   validated before anything is enqueued; returns 404 if the episode
    ///   isn't found, 200 (accepted) on success, or 400 with a reason when
    ///   the action's validation (or the caller's permission) rejects the
    ///   invocation.
    /// </summary>
    /// <param name="episodeId">Episode ID.</param>
    /// <param name="actionId">Action ID.</param>
    /// <param name="token">Cancellation token.</param>
    [HttpPost("{actionId:guid}")]
    public async Task<ActionResult> Invoke(
        [FromRoute, Range(1, int.MaxValue)] int episodeId,
        [FromRoute] Guid actionId,
        CancellationToken token
    )
    {
        if (actionService.GetActionInfo(actionId) is null)
            return NotFound("Action not found.");

        var episodeEntity = episodes.GetByID(episodeId);
        if (episodeEntity is null)
            return NotFound("Episode not found.");

        var validation = await actionService.InvokeAsync(actionId, episodeEntity, User, token);
        return validation is null ? Ok() : BadRequest(validation.Reason);
    }
}
