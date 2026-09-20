using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Video;

namespace Shoko.Abstractions.Actions.Services;

/// <summary>
///   Plugin-facing service for listing and invoking registered executable
///   actions.
/// </summary>
/// <remarks>
///   <para>
///     Actions are registered by implementing <see cref="IExecutableAction"/>
///     (or one of the scoped base classes) in the plugin assembly — they are
///     discovered and validated at plugin load. This service is how plugins
///     enumerate them and invoke them programmatically.
///   </para>
///   <para>
///     Invocation always goes through the job queue; <see cref="InvokeAsync(Guid,IReadOnlyDictionary{string,object},IUser,CancellationToken)"/>
///     and its overloads return <see langword="null"/> when the action was
///     accepted and enqueued, or a rejection reason when it was refused
///     without touching the queue (validation failure, permission denial, or
///     scope mismatch).
///   </para>
///   <para>
///     When <c>caller</c> is <see langword="null"/>, the call is
///     treated as a trusted programmatic invocation: the
///     <see cref="IExecutableAction.Permission"/> check is skipped. Actions
///     that implement <see cref="IActionCaller"/> require a non-null caller
///     and are rejected otherwise.
///   </para>
///   <para>
///     Invoking an action ID that is not registered throws
///     <see cref="KeyNotFoundException"/>.
///   </para>
/// </remarks>
public interface IActionService
{
    /// <summary>
    ///   Lists registered actions. <paramref name="scope"/> is a filter, not a
    ///   required partition — omitting it lists every action. When
    ///   <paramref name="callerPermission"/> is
    ///   <see cref="ActionPermission.User"/>, only actions invokable by a
    ///   regular user are returned.
    /// </summary>
    IReadOnlyList<ExecutableActionInfo> GetActions(ActionScope? scope = null, ActionPermission? callerPermission = null);

    /// <summary>
    ///   Gets the metadata for a registered action by its ID.
    /// </summary>
    ExecutableActionInfo? GetActionInfo(Guid actionId);

    /// <summary>
    ///   Checks an invocation payload against the action's parameter schema.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     For a caller holding a document it parsed rather than values it built
    ///     in code: an endpoint, core's own or a plugin's, that took a body and
    ///     wants to answer with errors the sender can attach to fields. An
    ///     in-process caller reaches for
    ///     <see cref="InvokeAsync(Guid, IReadOnlyDictionary{string, object?}, IUser?, CancellationToken)"/>
    ///     instead, since it passes a typed dictionary and the failure it wants
    ///     is a compiler error rather than a dictionary of paths.
    ///   </para>
    ///   <para>
    ///     The errors come back keyed by property path, which is the shape the
    ///     configuration endpoints already return for a rejected body, so the
    ///     two are surfaced the same way.
    ///   </para>
    /// </remarks>
    /// <param name="actionId">The action being invoked.</param>
    /// <param name="parameters">
    ///   The payload, or <see langword="null"/> when the caller sent no body.
    /// </param>
    /// <returns>Errors per property path; empty when the payload is acceptable.</returns>
    IReadOnlyDictionary<string, IReadOnlyList<string>> ValidateParameters(Guid actionId, JObject? parameters);

    /// <summary>
    ///   Gets the metadata for a registered action by its type, so a plugin
    ///   can find the ID of one of its own actions without deriving it.
    /// </summary>
    /// <typeparam name="TAction">
    ///   The action type.
    /// </typeparam>
    /// <returns>
    ///   The action's metadata, or <see langword="null"/> when the type is not
    ///   a registered action.
    /// </returns>
    ExecutableActionInfo? GetActionInfo<TAction>() where TAction : class, IExecutableAction;

    /// <summary>
    ///   Gets the metadata for a registered action by its type, so a plugin
    ///   can find the ID of one of its own actions without deriving it.
    /// </summary>
    /// <param name="actionType">
    ///   The action type.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="actionType"/> is <see langword="null"/>.
    /// </exception>
    /// <returns>
    ///   The action's metadata, or <see langword="null"/> when the type is not
    ///   a registered action.
    /// </returns>
    ExecutableActionInfo? GetActionInfo(Type actionType);

    /// <summary>
    ///   Invoke a global action by its ID.
    /// </summary>
    /// <param name="actionId">Action ID.</param>
    /// <param name="parameters">
    ///   Optional. The action's free-form invocation parameters. Each entry is populated
    ///   onto the matching public settable property of a fresh action instance
    ///   before it executes — the same way queue job properties are populated
    ///   from <c>JobDataJson</c>. Supported values are booleans, numbers, and
    ///   string lists; no nested objects. Entries with no matching property
    ///   are ignored.
    /// </param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    Task<ActionValidationResult?> InvokeAsync(Guid actionId, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Invoke a group-scoped action by its ID.
    /// </summary>
    /// <param name="actionId">Action ID.</param>
    /// <param name="group">The group to scope the action to.</param>
    /// <param name="parameters">Optional. The action's free-form invocation parameters. See the global overload for details.</param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    Task<ActionValidationResult?> InvokeAsync(Guid actionId, IShokoGroup group, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Invoke a series-scoped action by its ID.
    /// </summary>
    /// <param name="actionId">Action ID.</param>
    /// <param name="series">The series to scope the action to.</param>
    /// <param name="parameters">Optional. The action's free-form invocation parameters. See the global overload for details.</param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    Task<ActionValidationResult?> InvokeAsync(Guid actionId, IShokoSeries series, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Invoke an episode-scoped action by its ID.
    /// </summary>
    /// <param name="actionId">Action ID.</param>
    /// <param name="episode">The episode to scope the action to.</param>
    /// <param name="parameters">Optional. The action's free-form invocation parameters. See the global overload for details.</param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    Task<ActionValidationResult?> InvokeAsync(Guid actionId, IShokoEpisode episode, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Invoke a video-scoped action by its ID.
    /// </summary>
    /// <param name="actionId">Action ID.</param>
    /// <param name="video">The video to scope the action to.</param>
    /// <param name="parameters">Optional. The action's free-form invocation parameters. See the global overload for details.</param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    Task<ActionValidationResult?> InvokeAsync(Guid actionId, IVideo video, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Ask whether a global action would be accepted, without queuing it.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     The same checks <see cref="InvokeAsync(Guid, IReadOnlyDictionary{string, object?}, IUser?, CancellationToken)"/>
    ///     runs before it queues anything: that the action applies to this
    ///     scope, that the caller may invoke it, and the action's own
    ///     <see cref="IExecutableAction.Validate"/>. Nothing is queued either
    ///     way. This is how a client greys out an action it would only be told
    ///     about by invoking it and reading the rejection.
    ///   </para>
    ///   <para>
    ///     The answer is advisory. Nothing holds still between asking and
    ///     invoking, so an invocation may still be refused for a reason that
    ///     was not true a moment ago; invoke and handle the rejection rather
    ///     than trusting this and skipping it.
    ///   </para>
    /// </remarks>
    /// <param name="actionId">Action ID.</param>
    /// <param name="parameters">Optional. The parameters the invocation would carry, since the action's own validation observes them.</param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>
    ///   <see langword="null"/> when the action would be accepted, or the
    ///   reason it would be refused.
    /// </returns>
    Task<ActionValidationResult?> ValidateAsync(Guid actionId, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Ask whether a group-scoped action would be accepted for this group,
    ///   without queuing it. See the global overload for what is checked and
    ///   why the answer is advisory.
    /// </summary>
    /// <param name="actionId">Action ID.</param>
    /// <param name="group">The group the action would be scoped to.</param>
    /// <param name="parameters">Optional. The parameters the invocation would carry.</param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    Task<ActionValidationResult?> ValidateAsync(Guid actionId, IShokoGroup group, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Ask whether a series-scoped action would be accepted for this series,
    ///   without queuing it. See the global overload for what is checked and
    ///   why the answer is advisory.
    /// </summary>
    /// <param name="actionId">Action ID.</param>
    /// <param name="series">The series the action would be scoped to.</param>
    /// <param name="parameters">Optional. The parameters the invocation would carry.</param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    Task<ActionValidationResult?> ValidateAsync(Guid actionId, IShokoSeries series, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Ask whether an episode-scoped action would be accepted for this
    ///   episode, without queuing it. See the global overload for what is
    ///   checked and why the answer is advisory.
    /// </summary>
    /// <param name="actionId">Action ID.</param>
    /// <param name="episode">The episode the action would be scoped to.</param>
    /// <param name="parameters">Optional. The parameters the invocation would carry.</param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    Task<ActionValidationResult?> ValidateAsync(Guid actionId, IShokoEpisode episode, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Ask whether a video-scoped action would be accepted for this video,
    ///   without queuing it. See the global overload for what is checked and
    ///   why the answer is advisory.
    /// </summary>
    /// <param name="actionId">Action ID.</param>
    /// <param name="video">The video the action would be scoped to.</param>
    /// <param name="parameters">Optional. The parameters the invocation would carry.</param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    Task<ActionValidationResult?> ValidateAsync(Guid actionId, IVideo video, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default);





    /// <summary>
    ///   Invoke a group-scoped action across several groups at once, applying
    ///   it to all of them or to none.
    /// </summary>
    /// <remarks>
    ///   Every group is validated before any of them is queued, so a set that
    ///   contains one bad entry changes nothing. See the series overload for
    ///   what is reported and how.
    /// </remarks>
    /// <param name="actionId">Action ID.</param>
    /// <param name="groups">The groups to scope the action to, in the caller's own order.</param>
    /// <param name="parameters">Optional. The action's free-form invocation parameters, applied to every entry. See the global overload for details.</param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    /// <exception cref="Exceptions.GenericValidationException">
    ///   One or more entries were rejected. Nothing was queued.
    /// </exception>
    Task InvokeBulkAsync(Guid actionId, IReadOnlyList<IShokoGroup> groups, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Invoke a series-scoped action across several series at once, applying
    ///   it to all of them or to none.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Every entry is validated first, and the action is queued for all of
    ///     them only if all of them passed. A set containing one entry the
    ///     action refuses therefore changes nothing, which is the point: a
    ///     caller applying an action to a selection wants to fix the selection
    ///     and retry, not discover afterwards which half of it ran.
    ///   </para>
    ///   <para>
    ///     Whether the action is registered, applies to this scope, and is one
    ///     the caller may invoke are properties of the action rather than of
    ///     any entry, so they fail the call as a whole rather than being
    ///     reported once per entry.
    ///   </para>
    ///   <para>
    ///     Queuing is all this does, as with the single-entry overloads. An
    ///     entry that fails once it reaches the front of the queue is a failed
    ///     job and is reported as one; it does not reach back into this call.
    ///   </para>
    /// </remarks>
    /// <param name="actionId">Action ID.</param>
    /// <param name="series">The series to scope the action to, in the caller's own order.</param>
    /// <param name="parameters">Optional. The action's free-form invocation parameters, applied to every entry. See the global overload for details.</param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    /// <exception cref="Exceptions.GenericValidationException">
    ///   One or more entries were rejected, or the action itself was. Nothing
    ///   was queued. An entry's failure is keyed <c>IDs[i]</c>, where <c>i</c>
    ///   is its position in <paramref name="series"/>, so a caller can map each
    ///   failure back to what it passed; a failure of the action as a whole is
    ///   keyed by the empty string.
    /// </exception>
    Task InvokeBulkAsync(Guid actionId, IReadOnlyList<IShokoSeries> series, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Invoke an episode-scoped action across several episodes at once,
    ///   applying it to all of them or to none.
    /// </summary>
    /// <remarks>
    ///   Every episode is validated before any of them is queued, so a set
    ///   that contains one bad entry changes nothing. See the series overload
    ///   for what is reported and how.
    /// </remarks>
    /// <param name="actionId">Action ID.</param>
    /// <param name="episodes">The episodes to scope the action to, in the caller's own order.</param>
    /// <param name="parameters">Optional. The action's free-form invocation parameters, applied to every entry. See the global overload for details.</param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    /// <exception cref="Exceptions.GenericValidationException">
    ///   One or more entries were rejected. Nothing was queued.
    /// </exception>
    Task InvokeBulkAsync(Guid actionId, IReadOnlyList<IShokoEpisode> episodes, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Invoke a video-scoped action across several videos at once, applying
    ///   it to all of them or to none.
    /// </summary>
    /// <remarks>
    ///   Every video is validated before any of them is queued, so a set that
    ///   contains one bad entry changes nothing. See the series overload for
    ///   what is reported and how.
    /// </remarks>
    /// <param name="actionId">Action ID.</param>
    /// <param name="videos">The videos to scope the action to, in the caller's own order.</param>
    /// <param name="parameters">Optional. The action's free-form invocation parameters, applied to every entry. See the global overload for details.</param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    /// <exception cref="Exceptions.GenericValidationException">
    ///   One or more entries were rejected. Nothing was queued.
    /// </exception>
    Task InvokeBulkAsync(Guid actionId, IReadOnlyList<IVideo> videos, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default);
}
