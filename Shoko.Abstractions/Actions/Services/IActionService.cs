using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
///     Invocation always goes through the job queue; <see cref="InvokeAsync(System.Guid,Shoko.Abstractions.User.IUser,System.Threading.CancellationToken)"/>
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
    ///   Invoke a global action by its ID.
    /// </summary>
    /// <param name="actionId">Action ID.</param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    Task<ActionValidationResult?> InvokeAsync(Guid actionId, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Invoke a global action by its ID with free-form invocation parameters.
    /// </summary>
    /// <param name="actionId">Action ID.</param>
    /// <param name="parameters">
    ///   The action's free-form invocation parameters. Each entry is populated
    ///   onto the matching public settable property of a fresh action instance
    ///   before it executes — the same way queue job properties are populated
    ///   from <c>JobDataJson</c>. Supported values are booleans, numbers, and
    ///   string lists; no nested objects. Entries with no matching property
    ///   are ignored.
    /// </param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    Task<ActionValidationResult?> InvokeAsync(Guid actionId, IReadOnlyDictionary<string, object?> parameters, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Invoke a group-scoped action by its ID.
    /// </summary>
    /// <param name="actionId">Action ID.</param>
    /// <param name="group">The group to scope the action to.</param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    Task<ActionValidationResult?> InvokeAsync(Guid actionId, IShokoGroup group, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Invoke a group-scoped action by its ID with free-form invocation parameters.
    /// </summary>
    /// <param name="actionId">Action ID.</param>
    /// <param name="group">The group to scope the action to.</param>
    /// <param name="parameters">The action's free-form invocation parameters. See the global overload for details.</param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    Task<ActionValidationResult?> InvokeAsync(Guid actionId, IShokoGroup group, IReadOnlyDictionary<string, object?> parameters, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Invoke a series-scoped action by its ID.
    /// </summary>
    /// <param name="actionId">Action ID.</param>
    /// <param name="series">The series to scope the action to.</param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    Task<ActionValidationResult?> InvokeAsync(Guid actionId, IShokoSeries series, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Invoke a series-scoped action by its ID with free-form invocation parameters.
    /// </summary>
    /// <param name="actionId">Action ID.</param>
    /// <param name="series">The series to scope the action to.</param>
    /// <param name="parameters">The action's free-form invocation parameters. See the global overload for details.</param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    Task<ActionValidationResult?> InvokeAsync(Guid actionId, IShokoSeries series, IReadOnlyDictionary<string, object?> parameters, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Invoke an episode-scoped action by its ID.
    /// </summary>
    /// <param name="actionId">Action ID.</param>
    /// <param name="episode">The episode to scope the action to.</param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    Task<ActionValidationResult?> InvokeAsync(Guid actionId, IShokoEpisode episode, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Invoke an episode-scoped action by its ID with free-form invocation parameters.
    /// </summary>
    /// <param name="actionId">Action ID.</param>
    /// <param name="episode">The episode to scope the action to.</param>
    /// <param name="parameters">The action's free-form invocation parameters. See the global overload for details.</param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    Task<ActionValidationResult?> InvokeAsync(Guid actionId, IShokoEpisode episode, IReadOnlyDictionary<string, object?> parameters, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Invoke a video-scoped action by its ID.
    /// </summary>
    /// <param name="actionId">Action ID.</param>
    /// <param name="video">The video to scope the action to.</param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    Task<ActionValidationResult?> InvokeAsync(Guid actionId, IVideo video, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Invoke a video-scoped action by its ID with free-form invocation parameters.
    /// </summary>
    /// <param name="actionId">Action ID.</param>
    /// <param name="video">The video to scope the action to.</param>
    /// <param name="parameters">The action's free-form invocation parameters. See the global overload for details.</param>
    /// <param name="caller">The invoking user, or <see langword="null"/> for a trusted programmatic call.</param>
    /// <param name="token">Cancellation token.</param>
    Task<ActionValidationResult?> InvokeAsync(Guid actionId, IVideo video, IReadOnlyDictionary<string, object?> parameters, IUser? caller = null, CancellationToken token = default);
}
