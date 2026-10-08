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
using Shoko.Abstractions.UI.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Action;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/Series/{seriesID:int}/Action"), Tags("Action")]
[ApiV3]
[Authorize]
public class SeriesActionController(IActionService actionService, AnimeSeriesRepository series, ISettingsProvider settingsProvider) : BaseController(settingsProvider)
{
    /// <summary>
    ///   Invoke a series-scoped action by its ID. Entity existence is
    ///   validated before anything is enqueued; returns 404 if the series
    ///   isn't found, 200 (accepted) on success, or 400 with a reason when
    ///   the action's validation (or the caller's permission) rejects the
    ///   invocation.
    /// </summary>
    /// <param name="seriesID">Series ID.</param>
    /// <param name="actionID">Action ID.</param>
    /// <param name="parameters">
    ///   Optional. The action's invocation parameters. Omit the body entirely
    ///   for an action that takes none.
    /// </param>
    /// <param name="token">Cancellation token.</param>
    [HttpPost("{actionID:guid}")]
    public async Task<ActionResult> Invoke(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] Guid actionID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JObject? parameters,
        CancellationToken token
    )
    {
        if (actionService.GetActionInfo(actionID) is null)
            return NotFound("Action not found.");

        var seriesEntity = series.GetByID(seriesID);
        if (seriesEntity is null)
            return NotFound("Series not found.");

        if (actionService.ValidateParameters(actionID, parameters) is { Count: > 0 } errors)
            return ValidationProblem(errors);

        // Parameters are an argument like any other now, and null is what an
        // action taking none has always been invoked with.
        var validation = await actionService.InvokeAsync(actionID, seriesEntity, parameters.ToParameters(), caller: User, token: token);
        return validation is null ? Ok() : BadRequest(validation.Reason);
    }

    /// <summary>
    ///   List the options the server offers for one of a series-scoped
    ///   action's parameters, as the parameter's <c>OptionsRoute</c> says to.
    /// </summary>
    /// <param name="seriesID">Series ID.</param>
    /// <param name="actionID">Action ID.</param>
    /// <param name="parameters">Optional. The parameters entered so far.</param>
    /// <param name="target">The part of the member to list for: its values by default, or <c>Keys</c> for a dictionary's keys.</param>
    /// <param name="path">Path to the parameter, the same path a configuration's custom action is invoked with.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>The options, in the order the provider listed them.</returns>
    [HttpPost("{actionID:guid}/Options")]
    [HttpPost("{actionID:guid}/Options/{target}")]
    public async Task<ActionResult<IReadOnlyList<UiOption>>> GetOptions(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] Guid actionID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JObject? parameters,
        [FromQuery] string path = "",
        [FromRoute] OptionsTarget target = OptionsTarget.Values,
        CancellationToken token = default
    )
    {
        if (actionService.GetActionInfo(actionID) is null)
            return NotFound("Action not found.");

        var seriesEntity = series.GetByID(seriesID);
        if (seriesEntity is null)
            return NotFound("Series not found.");

        if (actionService.ValidateParameters(actionID, parameters) is { Count: > 0 } errors)
            return ValidationProblem(errors);

        try
        {
            return Ok(await actionService.GetParameterOptionsAsync(actionID, seriesEntity, path, target, parameters.ToParameters(), User, token));
        }
        catch (GenericValidationException ex)
        {
            return ValidationProblem(ex.ValidationErrors);
        }
    }
}
