using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.User;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Actions;

/// <summary>
///   The one generic wrapper job that executes every registered action.
///   Enqueued by <see cref="ActionService.InvokeAsync(Guid, System.Collections.Generic.IReadOnlyDictionary{string, object}, IUser?, System.Threading.CancellationToken)"/> with the action ID,
///   scope, scope entity ID, and calling user ID populated from
///   <c>JobDataJson</c> — the same mechanism every other
///   <see cref="IQueueJob"/> in this codebase already uses.
/// </summary>
[DatabaseRequired]
[JobKeyMember("Action")]
[JobKeyGroup(JobKeyGroup.Actions)]
public class ActionExecutionJob(
    IServiceProvider services,
    ActionService actionService,
    JMMUserRepository users,
    AnimeSeriesRepository series,
    AnimeGroupRepository groups,
    AnimeEpisodeRepository episodes,
    VideoLocalRepository videos,
    IJobCancellationAccessor cancellation,
    IJobProgressAccessor progress
) : BaseJob
{
    [JobKeyMember(index: 0)]
    public Guid ActionId { get; set; }

    /// <summary>
    ///   The scope of the action, mirrored from
    ///   <see cref="ExecutableActionInfo.Scope"/> at enqueue time.
    /// </summary>
    public ActionScope Scope { get; set; }

    /// <summary>
    ///   The ID of the entity to scope the action to, if applicable.
    /// </summary>
    [JobKeyMember(index: 1)]
    public int? ScopeEntityId { get; set; }

    /// <summary>
    ///   The ID of the user that invoked the action, or 0 for a trusted call
    ///   with no caller.
    /// </summary>
    [JobKeyMember(index: 2)]
    public int CallerUserId { get; set; }

    /// <summary>
    ///   The action's free-form invocation parameters (the open-ended invocation
    ///   parameter case), populated onto the matching public settable properties
    ///   of the action instance before it executes.
    /// </summary>
    /// <remarks>
    ///   Part of the dedup key, so the same action invoked on the same scope
    ///   and caller with different parameters still enqueues instead of
    ///   collapsing into an already-queued job.
    /// </remarks>
    [JobKeyMember(index: 3)]
    public Dictionary<string, object?>? Parameters { get; set; }

    public override string TypeName => "Action Execution";

    public override string Title => actionService.GetActionName(ActionId);

    public override Dictionary<string, object> Details => new()
    {
        { "Action", actionService.GetActionName(ActionId) },
        { "Scope", Scope },
    };

    public override async Task Execute()
    {
        var info = actionService.GetActionInfo(ActionId);
        if (info is null)
        {
            _logger.LogWarning("ActionExecutionJob: Action not found: {ActionId}", ActionId);
            return;
        }

        _logger.LogInformation("Executing action \"{ActionName}\" ({ActionId})", info.Name, ActionId);

        // Transient — a fresh instance per execution, resolved from DI.
        var action = (IExecutableAction)services.GetRequiredService(actionService.GetActionType(ActionId));

        // Populate the action's free-form properties from JobDataJson before it
        // executes — the same mechanism every other job property already uses.
        // Unknown property names are ignored.
        ActionService.PopulateParameters(action, Parameters);

        object? entity = null;
        if (action is IScopedAction scoped)
        {
            // No entity ID on a scoped action is a bug in whatever enqueued it, not a
            // condition that changed, so it throws rather than being skipped.
            if (ScopeEntityId is not { } entityId)
                throw new InvalidOperationException($"Scoped action '{info.Name}' ({info.ID}) has no scope entity ID.");

            entity = ResolveScopeEntity(info.Scope, entityId);
            if (entity is null)
            {
                _logger.LogWarning("Skipping action \"{ActionName}\" ({ActionId}): the {Scope} it was queued for ({EntityId}) no longer exists", info.Name, ActionId, info.Scope, entityId);
                return;
            }

            scoped.SetContext(entity);
        }

        if (CallerUserId > 0)
        {
            if (users.GetByID(CallerUserId) is not { } caller)
            {
                _logger.LogWarning("Skipping action \"{ActionName}\" ({ActionId}): the user that queued it ({UserId}) no longer exists", info.Name, ActionId, CallerUserId);
                return;
            }

            // What the caller may see can change while the job waits.
            if (ActionService.CheckVisible(action, entity, caller) is { } hidden)
            {
                _logger.LogWarning("Skipping action \"{ActionName}\" ({ActionId}): {Reason}", info.Name, ActionId, hidden.Reason);
                return;
            }

            if (action is IActionCaller callerAware)
                callerAware.SetCaller(caller);
        }

        // Validate ran before this job was enqueued, and that answer can be hours old
        // by the time the job reaches the front of the queue. What actions check is
        // exactly the sort of thing that changes in between — auto-matching still being
        // enabled, a file still having a location, a user still being linked to AniDB —
        // so it is asked again here, against the state the action is about to act on.
        if (await action.Validate(cancellation.Token) is { } validation)
        {
            _logger.LogWarning("Skipping action \"{ActionName}\" ({ActionId}): {Reason}", info.Name, ActionId, validation.Reason);
            return;
        }

        if (action is IProgressReportingAction reporting)
            reporting.SetProgress(progress.Progress);

        // The job's own token: no HTTP request is left by the time a queued action runs.
        await action.Execute(cancellation.Token);

        _logger.LogInformation("Finished executing action \"{ActionName}\" ({ActionId})", info.Name, ActionId);
    }

    /// <summary>
    ///   Looks up the entity the action was queued for.
    /// </summary>
    /// <remarks>
    ///   Returns <see langword="null"/> when it is gone rather than throwing:
    ///   an entity deleted between enqueue and execution is permanent, and
    ///   throwing would spend the job's whole retry budget rediscovering that.
    /// </remarks>
    private object? ResolveScopeEntity(ActionScope scope, int entityId) => scope switch
    {
        ActionScope.Series => series.GetByID(entityId),
        ActionScope.Group => groups.GetByID(entityId),
        ActionScope.Episode => episodes.GetByID(entityId),
        ActionScope.Video => videos.GetByID(entityId),
        _ => throw new InvalidOperationException("Global actions have no scope entity."),
    };
}
