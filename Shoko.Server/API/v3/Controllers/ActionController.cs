using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.UI;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Action;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]"), Tags("Action")]
[ApiV3]
[Authorize]
public class ActionController(
    ActionService actionService,
    AnimeGroupRepository groups,
    AnimeSeriesRepository series,
    AnimeEpisodeRepository episodes,
    VideoLocalRepository videos,
    ISettingsProvider settingsProvider
) : BaseController(settingsProvider)
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
    ///   Get a render-ready UI definition for the parameters of the action with
    ///   the given ID, in the same shape a configuration editor is described by.
    /// </summary>
    /// <remarks>
    ///   One endpoint covers every scope: an action's parameters come off the
    ///   action type, which does not vary by the entity it is invoked against,
    ///   so a series-scoped action is described here the same as a global one.
    ///   Ask only when the listing said <see cref="ActionInfo.HasParameters"/>.
    /// </remarks>
    /// <param name="actionID">Action ID.</param>
    /// <returns>The UI definition for the action's parameters.</returns>
    [HttpGet("{actionID:guid}/UiDefinition")]
    public ActionResult<UiDefinition> GetActionUiDefinition([FromRoute] Guid actionID)
    {
        if (actionService.GetActionInfo(actionID) is not { } info)
            return NotFound("Action not found.");

        if (info.Parameters is not { } parameters)
            return NotFound("Action does not take any parameters.");

        return parameters;
    }

    /// <summary>
    ///   Invoke a global action by its ID. Returns 200 (accepted), or 400 with
    ///   a reason when the action's validation (or the caller's permission)
    ///   rejects the invocation.
    /// </summary>
    /// <param name="actionID">Action ID.</param>
    /// <param name="parameters">
    ///   Optional. The action's invocation parameters. Omit the body entirely
    ///   for an action that takes none.
    /// </param>
    /// <param name="token">Cancellation token.</param>
    [HttpPost("{actionID:guid}")]
    public async Task<ActionResult> Invoke(
        [FromRoute] Guid actionID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JObject? parameters,
        CancellationToken token
    )
    {
        if (actionService.GetActionInfo(actionID) is null)
            return NotFound("Action not found.");

        if (actionService.ValidateParameters(actionID, parameters) is { Count: > 0 } errors)
            return ValidationProblem(errors);

        // Parameters are an argument like any other now, and null is what an
        // action taking none has always been invoked with.
        var validation = await actionService.InvokeAsync(actionID, parameters.ToParameters(), caller: User, token: token);
        return validation is null ? Ok() : BadRequest(validation.Reason);
    }

    #region Bulk

    /// <summary>
    ///   Invoke a group-scoped action across several groups at once. Applies
    ///   to all of them or to none.
    /// </summary>
    /// <param name="actionID">Action ID.</param>
    /// <param name="body">The group IDs, and optional invocation parameters applied to every one of them.</param>
    /// <param name="token">Cancellation token.</param>
    [HttpPost("{actionID:guid}/Group/Bulk")]
    public Task<ActionResult> InvokeBulkForGroups(
        [FromRoute] Guid actionID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ActionBulkInvokeBody body,
        CancellationToken token
    ) => InvokeBulk(actionID, body, groups.GetByID, (entities, parameters) => actionService.InvokeBulkAsync(actionID, entities, parameters, User, token));

    /// <summary>
    ///   Invoke a series-scoped action across several series at once. Applies
    ///   to all of them or to none.
    /// </summary>
    /// <param name="actionID">Action ID.</param>
    /// <param name="body">The series IDs, and optional invocation parameters applied to every one of them.</param>
    /// <param name="token">Cancellation token.</param>
    [HttpPost("{actionID:guid}/Series/Bulk")]
    public Task<ActionResult> InvokeBulkForSeries(
        [FromRoute] Guid actionID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ActionBulkInvokeBody body,
        CancellationToken token
    ) => InvokeBulk(actionID, body, series.GetByID, (entities, parameters) => actionService.InvokeBulkAsync(actionID, entities, parameters, User, token));

    /// <summary>
    ///   Invoke an episode-scoped action across several episodes at once.
    ///   Applies to all of them or to none.
    /// </summary>
    /// <param name="actionID">Action ID.</param>
    /// <param name="body">The episode IDs, and optional invocation parameters applied to every one of them.</param>
    /// <param name="token">Cancellation token.</param>
    [HttpPost("{actionID:guid}/Episode/Bulk")]
    public Task<ActionResult> InvokeBulkForEpisodes(
        [FromRoute] Guid actionID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ActionBulkInvokeBody body,
        CancellationToken token
    ) => InvokeBulk(actionID, body, episodes.GetByID, (entities, parameters) => actionService.InvokeBulkAsync(actionID, entities, parameters, User, token));

    /// <summary>
    ///   Invoke a video-scoped action across several files at once. Applies to
    ///   all of them or to none.
    /// </summary>
    /// <param name="actionID">Action ID.</param>
    /// <param name="body">The file IDs, and optional invocation parameters applied to every one of them.</param>
    /// <param name="token">Cancellation token.</param>
    [HttpPost("{actionID:guid}/File/Bulk")]
    public Task<ActionResult> InvokeBulkForFiles(
        [FromRoute] Guid actionID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] ActionBulkInvokeBody body,
        CancellationToken token
    ) => InvokeBulk(actionID, body, videos.GetByID, (entities, parameters) => actionService.InvokeBulkAsync(actionID, entities, parameters, User, token));

    /// <summary>
    ///   The shared half of the four bulk endpoints: resolve the IDs, refuse
    ///   the call if any of them names nothing, then hand the entities to the
    ///   service.
    /// </summary>
    /// <remarks>
    ///   An unresolvable ID stops the call before the service is reached,
    ///   because an entity that does not exist cannot be validated. The client
    ///   still learns about every bad ID at once, just not mixed in with the
    ///   action's own rejections.
    /// </remarks>
    /// <typeparam name="TEntity">The entity type the action is scoped to.</typeparam>
    /// <param name="actionID">Action ID.</param>
    /// <param name="body">The request body.</param>
    /// <param name="resolve">Looks one entity up by its ID.</param>
    /// <param name="invoke">Hands the resolved entities to the matching service overload.</param>
    private async Task<ActionResult> InvokeBulk<TEntity>(
        Guid actionID,
        ActionBulkInvokeBody body,
        Func<int, TEntity?> resolve,
        Func<IReadOnlyList<TEntity>, IReadOnlyDictionary<string, object?>?, Task> invoke
    ) where TEntity : class
    {
        if (actionService.GetActionInfo(actionID) is null)
            return NotFound("Action not found.");

        var entities = new List<TEntity>(body.IDs.Count);
        var unresolved = new Dictionary<string, IReadOnlyList<string>>();
        for (var index = 0; index < body.IDs.Count; index++)
        {
            if (resolve(body.IDs[index]) is { } entity)
                entities.Add(entity);
            else
                unresolved[string.Create(CultureInfo.InvariantCulture, $"IDs[{index}]")] = ["No entity found with the given ID."];
        }

        if (unresolved.Count > 0)
            return ValidationProblem(unresolved);

        try
        {
            await invoke(entities, body.Parameters);
        }
        catch (GenericValidationException ex)
        {
            return ValidationProblem(ex.ValidationErrors);
        }

        return Ok();
    }

    #endregion
}
