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
    ///   The name of the action.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///   The description of the action.
    /// </summary>
    public string? Description { get => null; }

    /// <summary>
    ///   The category of the action.
    /// </summary>
    public ActionCategory Category { get => ActionCategory.PluginInferred; }

    /// <summary>
    ///   Indicates whether the action requires explicit user confirmation
    ///   before it can be executed. Destructive or irreversible actions
    ///   should return <c>true</c>.
    /// </summary>
    public bool RequiresConfirmation { get => false; }
}

/// <summary>
///   A global administrative action.
/// </summary>
public interface IExecutableGlobalAction : IExecutableAction
{
    /// <summary>
    ///   Execute the global administrative action.
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
///   A global user action.
/// </summary>
public interface IExecutableGlobalUserAction : IExecutableAction
{
    /// <summary>
    ///   Execute the global user action.
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
///   A group-level administrative action.
/// </summary>
public interface IExecutableGroupAction : IExecutableAction
{
    /// <summary>
    ///   Execute the group-level administrative action.
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
///   A group-level user action.
/// </summary>
public interface IExecutableGroupUserAction : IExecutableAction
{
    /// <summary>
    ///   Execute the group-level user action.
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
///   A series-level administrative action.
/// </summary>
public interface IExecutableSeriesAction : IExecutableAction
{
    /// <summary>
    ///   Execute the series-level administrative action.
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
///   A series-level user action.
/// </summary>
public interface IExecutableSeriesUserAction : IExecutableAction
{
    /// <summary>
    ///   Execute the series-level user action.
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
///   An episode-level administrative action.
/// </summary>
public interface IExecutableEpisodeAction : IExecutableAction
{
    /// <summary>
    ///   Execute the episode-level administrative action.
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
///   An episode-level user action.
/// </summary>
public interface IExecutableEpisodeUserAction : IExecutableAction
{
    /// <summary>
    ///   Execute the episode-level user action.
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
