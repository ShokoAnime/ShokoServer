using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;

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
///     When <paramref name="caller"/> is <see langword="null"/>, the call is
///     treated as a trusted programmatic invocation: the
///     <see cref="IExecutableAction.Permission"/> check is skipped. Actions
///     that implement <see cref="IActionCaller"/> require a non-null caller
///     and are rejected otherwise.
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
    Task<ActionValidationResult?> InvokeAsync(Guid actionId, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Invoke a group-scoped action by its ID.
    /// </summary>
    Task<ActionValidationResult?> InvokeAsync(Guid actionId, IShokoGroup group, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Invoke a series-scoped action by its ID.
    /// </summary>
    Task<ActionValidationResult?> InvokeAsync(Guid actionId, IShokoSeries series, IUser? caller = null, CancellationToken token = default);

    /// <summary>
    ///   Invoke an episode-scoped action by its ID.
    /// </summary>
    Task<ActionValidationResult?> InvokeAsync(Guid actionId, IShokoEpisode episode, IUser? caller = null, CancellationToken token = default);
}
