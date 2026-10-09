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
[Route("/api/v{version:apiVersion}/Episode/{episodeID:int}/Action"), Tags("Action")]
[ApiV3]
[Authorize]
public class EpisodeActionController(IActionService actionService, AnimeEpisodeRepository episodes, ISettingsProvider settingsProvider) : BaseController(settingsProvider)
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

        // Parameters are an argument like any other now, and null is what an
        // action taking none has always been invoked with.
        try
        {
            var validation = await actionService.InvokeAsync(actionID, episodeEntity, parameters.ToParameters(), caller: User, token: token);
            return validation is null ? Ok() : BadRequest(validation.Reason);
        }
        catch (GenericValidationException ex)
        {
            return ValidationProblem(ex.ValidationErrors);
        }
    }

    /// <summary>
    ///   List the options the server offers for one of a episode-scoped
    ///   action's parameters, when the parameter's <c>HasOptions</c> is set.
    /// </summary>
    /// <param name="episodeID">Episode ID.</param>
    /// <param name="actionID">Action ID.</param>
    /// <param name="parameters">Optional. The parameters entered so far.</param>
    /// <param name="path">
    ///   Path to the parameter, the same path a configuration's custom action
    ///   is invoked with. A dictionary's own path lists its keys, and the path
    ///   of one of its entries, such as <c>Weights["key"]</c>, the values for
    ///   that key.
    /// </param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>The options, in the order the provider listed them.</returns>
    [HttpPost("{actionID:guid}/Options")]
    public async Task<ActionResult<IReadOnlyList<UiOption>>> GetOptions(
        [FromRoute, Range(1, int.MaxValue)] int episodeID,
        [FromRoute] Guid actionID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JObject? parameters,
        [FromQuery] string path = "",
        CancellationToken token = default
    )
    {
        if (actionService.GetActionInfo(actionID) is null)
            return NotFound("Action not found.");

        var episodeEntity = episodes.GetByID(episodeID);
        if (episodeEntity is null)
            return NotFound("Episode not found.");

        if (actionService.ValidateParameters(actionID, parameters) is { Count: > 0 } errors)
            return ValidationProblem(errors);

        try
        {
            return Ok(await actionService.GetParameterOptionsAsync(actionID, episodeEntity, path, parameters.ToParameters(), User, token));
        }
        catch (GenericValidationException ex)
        {
            return ValidationProblem(ex.ValidationErrors);
        }
    }
}
