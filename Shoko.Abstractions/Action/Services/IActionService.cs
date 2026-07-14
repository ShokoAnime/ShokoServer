
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Action.Services;

/// <summary>
///   Service for registering, discovering, and executing plugin-provided actions.
///   Actions are registered during plugin initialization via <see cref="AddParts"/>
///   and can be executed immediately or scheduled through the job queue.
/// </summary>
public interface IActionService
{
    /// <summary>
    ///   Registers a collection of executable actions. Called once during plugin
    ///   initialization. Subsequent calls are no-ops.
    /// </summary>
    /// <param name="actions">The executable actions to register.</param>
    void AddParts(IEnumerable<IExecutableAction> actions);

    /// <summary>
    ///   Gets the list of all registered actions, optionally filtered by scopes,
    ///   categories, or category names. Each enumerable is converted to a set
    ///   internally; actions matching at least one value in each non-null set
    ///   are returned.
    /// </summary>
    /// <param name="scopes">
    ///   Optional scope filter. Returns actions whose <see cref="ExecutableActionInfo.Scopes"/>
    ///   set overlaps with any value in the provided sequence.
    /// </param>
    /// <param name="categories">Optional category enum filter.</param>
    /// <param name="categoryNames">
    ///   Optional category name filter. Matches <see cref="ExecutableActionInfo.CategoryName"/>
    ///   case-insensitively. Useful for filtering by plugin name when
    ///   <see cref="ExecutableActionInfo.Category"/> is <see cref="ActionCategory.PluginInferred"/>.
    /// </param>
    /// <returns>A read-only list of matching <see cref="ExecutableActionInfo"/>.</returns>
    IReadOnlyList<ExecutableActionInfo> GetActions(IEnumerable<ActionScope>? scopes = null, IEnumerable<ActionCategory>? categories = null, IEnumerable<string>? categoryNames = null);

    /// <summary>
    ///   Gets information about a registered action by its ID.
    /// </summary>
    /// <param name="actionId">The action's stable identifier.</param>
    /// <returns>The action info, or <c>null</c> if no action with the given ID is registered.</returns>
    ExecutableActionInfo? GetActionById(Guid actionId);

    #region Global

    /// <summary>
    ///   Executes a global-scoped system-level administrative action
    ///   immediately and awaits completion.
    /// </summary>
    /// <param name="actionInfo">The action to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when <paramref name="actionInfo"/> does not support global scope
    ///   (its <see cref="ExecutableActionInfo.Scopes"/> set does not contain
    ///   <see cref="ActionScope.SystemAndGlobal"/>).
    /// </exception>
    Task ExecuteGlobalSystemAction(ExecutableActionInfo actionInfo, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Schedules a global-scoped system-level administrative action for later
    ///   execution through the job queue system.
    /// </summary>
    /// <param name="actionInfo">The action to schedule.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="prioritize">If <c>true</c>, the job is enqueued at maximum priority.</param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when <paramref name="actionInfo"/> does not support global scope
    ///   (its <see cref="ExecutableActionInfo.Scopes"/> set does not contain
    ///   <see cref="ActionScope.SystemAndGlobal"/>).
    /// </exception>
    Task ScheduleExecuteOfGlobalSystemAction(ExecutableActionInfo actionInfo, CancellationToken cancellationToken = default, bool prioritize = false);

    /// <summary>
    ///   Executes a global-scoped user-level user action immediately for the
    ///   specified user and awaits completion.
    /// </summary>
    /// <param name="actionInfo">The action to execute.</param>
    /// <param name="user">The user to run the action for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when <paramref name="actionInfo"/> does not support user scope
    ///   (its <see cref="ExecutableActionInfo.Scopes"/> set does not contain
    ///   <see cref="ActionScope.UserAndGlobal"/>).
    /// </exception>
    Task ExecuteGlobalUserAction(ExecutableActionInfo actionInfo, IUser user, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Schedules a global-scoped user-level user action for later execution
    ///   through the job queue system.
    /// </summary>
    /// <param name="actionInfo">The action to schedule.</param>
    /// <param name="user">The user to run the action for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="prioritize">If <c>true</c>, the job is enqueued at maximum priority.</param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when <paramref name="actionInfo"/> does not support user scope
    ///   (its <see cref="ExecutableActionInfo.Scopes"/> set does not contain
    ///   <see cref="ActionScope.UserAndGlobal"/>).
    /// </exception>
    Task ScheduleExecuteOfGlobalUserAction(ExecutableActionInfo actionInfo, IUser user, CancellationToken cancellationToken = default, bool prioritize = false);

    #endregion

    #region Group

    /// <summary>
    ///   Executes a group-scoped system-level administrative action immediately
    ///   for the specified group and awaits completion.
    /// </summary>
    /// <param name="actionInfo">The action to execute.</param>
    /// <param name="group">The group to run the action on.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when <paramref name="actionInfo"/> does not support group scope
    ///   (its <see cref="ExecutableActionInfo.Scopes"/> set does not contain
    ///   <see cref="ActionScope.SystemAndGroup"/>).
    /// </exception>
    Task ExecuteGroupSystemAction(ExecutableActionInfo actionInfo, IShokoGroup group, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Schedules a group-scoped system-level administrative action for later
    ///   execution through the job queue system.
    /// </summary>
    /// <param name="actionInfo">The action to schedule.</param>
    /// <param name="group">The group to run the action on.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="prioritize">If <c>true</c>, the job is enqueued at maximum priority.</param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when <paramref name="actionInfo"/> does not support group scope
    ///   (its <see cref="ExecutableActionInfo.Scopes"/> set does not contain
    ///   <see cref="ActionScope.SystemAndGroup"/>).
    /// </exception>
    Task ScheduleExecuteOfGroupSystemAction(ExecutableActionInfo actionInfo, IShokoGroup group, CancellationToken cancellationToken = default, bool prioritize = false);

    /// <summary>
    ///   Executes a group-scoped user-level user action immediately for the
    ///   specified group and user, and awaits completion.
    /// </summary>
    /// <param name="actionInfo">The action to execute.</param>
    /// <param name="group">The group to run the action on.</param>
    /// <param name="user">The user to run the action for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when <paramref name="actionInfo"/> does not support group user scope
    ///   (its <see cref="ExecutableActionInfo.Scopes"/> set does not contain
    ///   <see cref="ActionScope.UserAndGroup"/>).
    /// </exception>
    Task ExecuteGroupUserAction(ExecutableActionInfo actionInfo, IShokoGroup group, IUser user, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Schedules a group-scoped user action for later execution through the
    ///   job queue.
    /// </summary>
    /// <param name="actionInfo">The action to schedule.</param>
    /// <param name="group">The group to run the action on.</param>
    /// <param name="user">The user to run the action for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="prioritize">If <c>true</c>, the job is enqueued at maximum priority.</param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when <paramref name="actionInfo"/> does not support group user scope
    ///   (its <see cref="ExecutableActionInfo.Scopes"/> set does not contain
    ///   <see cref="ActionScope.UserAndGroup"/>).
    /// </exception>
    Task ScheduleExecuteOfGroupUserAction(ExecutableActionInfo actionInfo, IShokoGroup group, IUser user, CancellationToken cancellationToken = default, bool prioritize = false);

    #endregion

    #region Series

    /// <summary>
    ///   Executes a series-scoped system-level administrative action
    ///   immediately for the specified series and awaits completion.
    /// </summary>
    /// <param name="actionInfo">The action to execute.</param>
    /// <param name="series">The series to run the action on.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when <paramref name="actionInfo"/> does not support series scope
    ///   (its <see cref="ExecutableActionInfo.Scopes"/> set does not contain
    ///   <see cref="ActionScope.SystemAndSeries"/>).
    /// </exception>
    Task ExecuteSeriesSystemAction(ExecutableActionInfo actionInfo, IShokoSeries series, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Schedules a series-scoped system-level administrative action for later
    ///   execution through the job queue system.
    /// </summary>
    /// <param name="actionInfo">The action to schedule.</param>
    /// <param name="series">The series to run the action on.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="prioritize">If <c>true</c>, the job is enqueued at maximum priority.</param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when <paramref name="actionInfo"/> does not support series scope
    ///   (its <see cref="ExecutableActionInfo.Scopes"/> set does not contain
    ///   <see cref="ActionScope.SystemAndSeries"/>).
    /// </exception>
    Task ScheduleExecuteOfSeriesSystemAction(ExecutableActionInfo actionInfo, IShokoSeries series, CancellationToken cancellationToken = default, bool prioritize = false);

    /// <summary>
    ///   Executes a series-scoped user-level user action immediately for the
    ///   specified series and user, and awaits completion.
    /// </summary>
    /// <param name="actionInfo">The action to execute.</param>
    /// <param name="series">The series to run the action on.</param>
    /// <param name="user">The user to run the action for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when <paramref name="actionInfo"/> does not support series user scope
    ///   (its <see cref="ExecutableActionInfo.Scopes"/> set does not contain
    ///   <see cref="ActionScope.UserAndSeries"/>).
    /// </exception>
    Task ExecuteSeriesUserAction(ExecutableActionInfo actionInfo, IShokoSeries series, IUser user, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Schedules a series-scoped user-level user action for later execution
    ///   through the job queue system.
    /// </summary>
    /// <param name="actionInfo">The action to schedule.</param>
    /// <param name="series">The series to run the action on.</param>
    /// <param name="user">The user to run the action for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="prioritize">If <c>true</c>, the job is enqueued at maximum priority.</param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when <paramref name="actionInfo"/> does not support series user scope
    ///   (its <see cref="ExecutableActionInfo.Scopes"/> set does not contain
    ///   <see cref="ActionScope.UserAndSeries"/>).
    /// </exception>
    Task ScheduleExecuteOfSeriesUserAction(ExecutableActionInfo actionInfo, IShokoSeries series, IUser user, CancellationToken cancellationToken = default, bool prioritize = false);

    #endregion

    #region Episode

    /// <summary>
    ///   Executes an episode-scoped system-level administrative action
    ///   immediately for the specified episode and awaits completion.
    /// </summary>
    /// <param name="actionInfo">The action to execute.</param>
    /// <param name="episode">The episode to run the action on.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when <paramref name="actionInfo"/> does not support episode scope
    ///   (its <see cref="ExecutableActionInfo.Scopes"/> set does not contain
    ///   <see cref="ActionScope.Episode"/>).
    /// </exception>
    Task ExecuteEpisodeSystemAction(ExecutableActionInfo actionInfo, IShokoEpisode episode, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Schedules an episode-scoped system-level administrative action for
    ///   later execution through the job queue.
    /// </summary>
    /// <param name="actionInfo">The action to schedule.</param>
    /// <param name="episode">The episode to run the action on.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="prioritize">If <c>true</c>, the job is enqueued at maximum priority.</param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when <paramref name="actionInfo"/> does not support episode scope
    ///   (its <see cref="ExecutableActionInfo.Scopes"/> set does not contain
    ///   <see cref="ActionScope.Episode"/>).
    /// </exception>
    Task ScheduleExecuteOfEpisodeSystemAction(ExecutableActionInfo actionInfo, IShokoEpisode episode, CancellationToken cancellationToken = default, bool prioritize = false);

    /// <summary>
    ///   Executes an episode-scoped user-level user action immediately for the
    ///   specified episode and user, and awaits completion.
    /// </summary>
    /// <param name="actionInfo">The action to execute.</param>
    /// <param name="episode">The episode to run the action on.</param>
    /// <param name="user">The user to run the action for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when <paramref name="actionInfo"/> does not support episode user scope
    ///   (its <see cref="ExecutableActionInfo.Scopes"/> set does not contain
    ///   <see cref="ActionScope.UserAndEpisode"/>).
    /// </exception>
    Task ExecuteEpisodeUserAction(ExecutableActionInfo actionInfo, IShokoEpisode episode, IUser user, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Schedules an episode-scoped user-level user action for later execution
    ///   through the job queue system.
    /// </summary>
    /// <param name="actionInfo">The action to schedule.</param>
    /// <param name="episode">The episode to run the action on.</param>
    /// <param name="user">The user to run the action for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="prioritize">If <c>true</c>, the job is enqueued at maximum priority.</param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when <paramref name="actionInfo"/> does not support episode user scope
    ///   (its <see cref="ExecutableActionInfo.Scopes"/> set does not contain
    ///   <see cref="ActionScope.UserAndEpisode"/>).
    /// </exception>
    Task ScheduleExecuteOfEpisodeUserAction(ExecutableActionInfo actionInfo, IShokoEpisode episode, IUser user, CancellationToken cancellationToken = default, bool prioritize = false);

    #endregion
}
