using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json.Linq;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Action;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;
using Shoko.Server.Services;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/Episode/{episodeID:int}/Action")]
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
    /// <param name="episodeID">Episode ID.</param>
    /// <param name="actionID">Action ID.</param>
    /// <param name="parameters">
    ///   Optional. The action's invocation parameters. Omit the body entirely
    ///   for an action that takes none.
    /// </param>
    /// <param name="token">Cancellation token.</param>
    [HttpPost("{actionID:guid}")]
    public async Task<ActionResult> Invoke(
        [FromRoute, Range(1, int.MaxValue)] int episodeID,
        [FromRoute] Guid actionID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JObject? parameters,
        CancellationToken token
    )
    {
        if (actionService.GetActionInfo(actionID) is null)
            return NotFound("Action not found.");

        var episodeEntity = episodes.GetByID(episodeID);
        if (episodeEntity is null)
            return NotFound("Episode not found.");

        if (actionService.ValidateParameters(actionID, parameters) is { Count: > 0 } errors)
            return ValidationProblem(errors);

        // No body takes the same overload it always has, so an action that
        // declares no parameters is invoked exactly as before.
        var parameterMap = parameters.ToParameters();
        var validation = parameterMap is null
            ? await actionService.InvokeAsync(actionID, episodeEntity, User, token)
            : await actionService.InvokeAsync(actionID, episodeEntity, parameterMap, User, token);
        return validation is null ? Ok() : BadRequest(validation.Reason);
    }
}
