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
[Route("/api/v{version:apiVersion}/Series/{seriesId:int}/Action")]
[ApiV3]
[Authorize]
public class SeriesActionController(ActionService actionService, AnimeSeriesRepository series, ISettingsProvider settingsProvider) : BaseController(settingsProvider)
{
    /// <summary>
    ///   Invoke a series-scoped action by its ID. Entity existence is
    ///   validated before anything is enqueued; returns 404 if the series
    ///   isn't found, 200 (accepted) on success, or 400 with a reason when
    ///   the action's validation (or the caller's permission) rejects the
    ///   invocation.
    /// </summary>
    /// <param name="seriesId">Series ID.</param>
    /// <param name="actionId">Action ID.</param>
    /// <param name="token">Cancellation token.</param>
    [HttpPost("{actionId:guid}")]
    public async Task<ActionResult> Invoke(
        [FromRoute, Range(1, int.MaxValue)] int seriesId,
        [FromRoute] Guid actionId,
        CancellationToken token
    )
    {
        var seriesEntity = series.GetByID(seriesId);
        if (seriesEntity is null)
            return NotFound("Series not found.");

        var validation = await actionService.InvokeAsync(actionId, seriesEntity, User, token);
        return validation is null ? Ok() : BadRequest(validation.Reason);
    }
}
