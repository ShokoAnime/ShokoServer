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
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Actions;

/// <summary>
///   The one generic wrapper job that executes every registered action.
///   Enqueued by <see cref="ActionService.InvokeAsync"/> with the action ID,
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
    AnimeEpisodeRepository episodes
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
    ///   The ID of the user that invoked the action.
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

        if (action is IScopedAction scoped)
        {
            if (ScopeEntityId is not { } entityId)
                throw new InvalidOperationException($"Scoped action '{info.Name}' ({info.Id}) has no scope entity ID.");

            scoped.SetContext(ResolveScopeEntity(info.Scope, entityId));
        }

        if (CallerUserId > 0 && action is IActionCaller callerAware)
        {
            var caller = users.GetByID(CallerUserId)
                ?? throw new InvalidOperationException($"User not found: {CallerUserId}");
            callerAware.SetCaller(caller);
        }

        // Validate is NOT re-run here — it already ran synchronously in
        // ActionService.InvokeAsync before this job was even enqueued. Re-running it
        // here would be redundant and could observe different state than what the
        // caller saw when they got their 200.
        // The job's own lifetime token, not request-bound — there is no live
        // HTTP request left by the time a queued action runs.
        await action.Execute();

        _logger.LogInformation("Finished executing action \"{ActionName}\" ({ActionId})", info.Name, ActionId);
    }

    private object ResolveScopeEntity(ActionScope scope, int entityId) => scope switch
    {
        ActionScope.Series => series.GetByID(entityId) ?? throw new KeyNotFoundException($"Series not found: {entityId}"),
        ActionScope.Group => groups.GetByID(entityId) ?? throw new KeyNotFoundException($"Group not found: {entityId}"),
        ActionScope.Episode => episodes.GetByID(entityId) ?? throw new KeyNotFoundException($"Episode not found: {entityId}"),
        _ => throw new InvalidOperationException("Global actions have no scope entity."),
    };
}
