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
        try
        {
            var validation = await actionService.InvokeAsync(actionID, seriesEntity, parameters.ToParameters(), caller: User, token: token);
            return validation is null ? Ok() : BadRequest(validation.Reason);
        }
        catch (GenericValidationException ex)
        {
            return ValidationProblem(ex.ValidationErrors);
        }
    }

    /// <summary>
    ///   List the options the server offers for one of a series-scoped
    ///   action's parameters, when the parameter's <c>HasOptions</c> is set.
    /// </summary>
    /// <param name="seriesID">Series ID.</param>
    /// <param name="actionID">Action ID.</param>
    /// <param name="parameters">
    ///   Optional. The parameters entered so far, read leniently: only the
    ///   one the path names has to be valid.
    /// </param>
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
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] Guid actionID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JObject? parameters,
        [FromQuery] string path = "",
        CancellationToken token = default
    )
    {
        if (actionService.GetActionInfo(actionID) is null)
            return NotFound("Action not found.");

        var seriesEntity = series.GetByID(seriesID);
        if (seriesEntity is null)
            return NotFound("Series not found.");

        // Not validated as a whole: only the parameter asked about has to be readable.
        try
        {
            return Ok(await actionService.GetParameterOptionsAsync(actionID, seriesEntity, path, parameters.ToParameters(), User, token));
        }
        catch (GenericValidationException ex)
        {
            return ValidationProblem(ex.ValidationErrors);
        }
    }
}
