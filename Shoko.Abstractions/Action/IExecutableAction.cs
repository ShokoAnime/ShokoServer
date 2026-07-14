using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Action;

/// <summary>
///   Base interface for all executable actions.
/// </summary>
/// <remarks>
///   <para>
///     Each action's stable identifier (<see cref="ExecutableActionInfo.ID"/>)
///     is a UUIDv5 deterministically derived from the action class's
///     fully-qualified name using the plugin's ID as the UUIDv5 namespace.
///   </para>
///   <para>
///     <strong>This ID is not stable across class renames or namespace moves.</strong>
///     If a plugin author renames or moves the implementing class, the derived
///     UUID will change. Existing references to the old ID (e.g. stored
///     bookmarks or scheduled invocations) will no longer resolve, and the
///     action will appear as a new entry under the new ID. This is by design
///     — deriving from namespace + class name + plugin ID makes accidental
///     collisions between unrelated plugins extremely unlikely without
///     requiring an explicit, collision-managed key field.
///   </para>
/// </remarks>
public interface IExecutableAction
{
    /// <summary>
    ///   The name of the action. When <c>null</c>, the service derives a
    ///   display name from the implementing class's name automatically.
    /// </summary>
    public string? Name { get => null; }

    /// <summary>
    ///   The description of the action.
    /// </summary>
    public string? Description { get => null; }

    /// <summary>
    ///   The category of the action.
    /// </summary>
    public ActionCategory Category { get => ActionCategory.Mischievous; }

    /// <summary>
    ///   Indicates whether the action requires explicit user confirmation
    ///   before it can be executed. Destructive or irreversible actions
    ///   should return <c>true</c>.
    /// </summary>
    public bool RequiresConfirmation { get => false; }
}

/// <summary>
///   A global-scoped system-level administrative action.
/// </summary>
public interface IExecutableGlobalSystemAction : IExecutableAction
{
    /// <summary>
    ///   Execute the global-scoped system-level administrative action.
    /// </summary>
    /// <param name="cancellationToken">
    ///   The cancellation token.
    /// </param>
    /// <returns>
    ///   A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    Task Execute(CancellationToken cancellationToken = default);
}

/// <summary>
///   A global-scoped user-level user action.
/// </summary>
public interface IExecutableGlobalUserAction : IExecutableAction
{
    /// <summary>
    ///   Execute the global-scoped user-level user action.
    /// </summary>
    /// <param name="user">
    ///   The user to run the action for.
    /// </param>
    /// <param name="cancellationToken">
    ///   The cancellation token.
    /// </param>
    /// <returns>
    ///   A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    Task Execute(IUser user, CancellationToken cancellationToken = default);
}

/// <summary>
///   A group-scoped system-level administrative action.
/// </summary>
public interface IExecutableGroupSystemAction : IExecutableAction
{
    /// <summary>
    ///   Execute the group-scoped system-level administrative action.
    /// </summary>
    /// <param name="group">
    ///   The group to run the action on.
    /// </param>
    /// <param name="cancellationToken">
    ///   The cancellation token.
    /// </param>
    /// <returns>
    ///   A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    Task Execute(IShokoGroup group, CancellationToken cancellationToken = default);
}

/// <summary>
///   A group-scoped user-level user action.
/// </summary>
public interface IExecutableGroupUserAction : IExecutableAction
{
    /// <summary>
    ///   Execute the group-scoped user-level user action.
    /// </summary>
    /// <param name="group">
    ///   The group to run the action on.
    /// </param>
    /// <param name="user">
    ///   The user to run the action for.
    /// </param>
    /// <param name="cancellationToken">
    ///   The cancellation token.
    /// </param>
    /// <returns>
    ///   A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    Task Execute(IShokoGroup group, IUser user, CancellationToken cancellationToken = default);
}

/// <summary>
///   A series-scoped system-level administrative action.
/// </summary>
public interface IExecutableSeriesSystemAction : IExecutableAction
{
    /// <summary>
    ///   Execute the series-scoped system-level administrative action.
    /// </summary>
    /// <param name="series">
    ///   The series to run the action on.
    /// </param>
    /// <param name="cancellationToken">
    ///   The cancellation token.
    /// </param>
    /// <returns>
    ///   A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    Task Execute(IShokoSeries series, CancellationToken cancellationToken = default);
}

/// <summary>
///   A series-scoped user-level user action.
/// </summary>
public interface IExecutableSeriesUserAction : IExecutableAction
{
    /// <summary>
    ///   Execute the series-scoped user-level user action.
    /// </summary>
    /// <param name="series">
    ///   The series to run the action on.
    /// </param>
    /// <param name="user">
    ///   The user to run the action for.
    /// </param>
    /// <param name="cancellationToken">
    ///   The cancellation token.
    /// </param>
    /// <returns>
    ///   A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    Task Execute(IShokoSeries series, IUser user, CancellationToken cancellationToken = default);
}

/// <summary>
///   An episode-scoped system-level administrative action.
/// </summary>
public interface IExecutableEpisodeSystemAction : IExecutableAction
{
    /// <summary>
    ///   Execute the episode-scoped system-level administrative action.
    /// </summary>
    /// <param name="episode">
    ///   The episode to run the action on.
    /// </param>
    /// <param name="cancellationToken">
    ///   The cancellation token.
    /// </param>
    /// <returns>
    ///   A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    Task Execute(IShokoEpisode episode, CancellationToken cancellationToken = default);
}

/// <summary>
///   An episode-scoped user-level user action.
/// </summary>
public interface IExecutableEpisodeUserAction : IExecutableAction
{
    /// <summary>
    ///   Execute the episode-scoped user-level user action.
    /// </summary>
    /// <param name="episode">
    ///   The episode to run the action on.
    /// </param>
    /// <param name="user">
    ///   The user to run the action for.
    /// </param>
    /// <param name="cancellationToken">
    ///   The cancellation token.
    /// </param>
    /// <returns>
    ///   A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    Task Execute(IShokoEpisode episode, IUser user, CancellationToken cancellationToken = default);
}
