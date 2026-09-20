using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Abstractions.Actions;

/// <summary>
///   A discrete, invokable unit of work that can be registered by core or a
///   plugin, listed through the API, and executed through the job queue.
/// </summary>
/// <remarks>
///   <para>
///     Each action's stable identifier is a UUIDv5 deterministically derived
///     from the action class's fully-qualified name using the owning plugin's
///     ID as the UUIDv5 namespace.
///   </para>
///   <para>
///     <strong>This ID is not stable across class renames or namespace
///     moves.</strong> If a plugin author renames or moves the implementing
///     class, the derived UUID will change. This is by design — deriving from
///     namespace + class name + plugin ID makes accidental collisions between
///     unrelated plugins extremely unlikely without requiring an explicit,
///     collision-managed key field.
///   </para>
///   <para>
///     Every action is always executed through the job queue, so progress and
///     status are visible. Concurrency is handled by the queue system itself;
///     there is no per-action opt-in flag.
///   </para>
/// </remarks>
public interface IExecutableAction
{
    /// <summary>
    ///   The display name of the action.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   The description of the action.
    /// </summary>
    string? Description { get => null; }

    /// <summary>
    ///   The category of the action. Defaults to
    ///   <see cref="ActionCategory.Miscellaneous"/>, the shared fallback
    ///   category for any action that declares none.
    /// </summary>
    ActionCategory Category => ActionCategory.Miscellaneous;

    /// <summary>
    ///   Whether the action is prominent enough to offer on its own, rather
    ///   than inside the group its <see cref="Category"/> names. Defaults to
    ///   <see langword="false"/>.
    /// </summary>
    /// <remarks>
    ///   This says how prominently to offer the action, not what the action
    ///   is about, so it is independent of <see cref="Category"/>: an action
    ///   keeps the category it belongs to whether or not it is promoted. A
    ///   client is free to ignore the flag, and one with no room for the
    ///   distinction should.
    /// </remarks>
    bool IsPrimaryAction => false;

    /// <summary>
    ///   The permission required to invoke the action.
    /// </summary>
    /// <remarks>
    ///   There is deliberately no default implementation — every action must
    ///   state its permission explicitly. The action registry rejects at load
    ///   time any action type that does not declare it on the type itself.
    /// </remarks>
    ActionPermission Permission { get; }

    /// <summary>
    ///   Whether this action requires confirmation before invocation.
    /// </summary>
    bool RequiresConfirmation { get => false; }

    /// <summary>
    ///   Optional custom message shown to the user when the WebUI prompts for
    ///   confirmation before invoking a destructive action. When
    ///   <see langword="null"/>, the WebUI falls back to a generic prompt.
    ///   Only meaningful when <see cref="RequiresConfirmation"/> is
    ///   <see langword="true"/>.
    /// </summary>
    string? ConfirmationMessage { get => null; }

    /// <summary>
    ///   Optional pre-check. Return a non-null result to refuse. Default:
    ///   always allowed.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Asked <b>twice</b>, on two different instances. Once before the
    ///     action is enqueued, so a caller is refused immediately (e.g. HTTP
    ///     400) rather than handed a job that was never going to work; and
    ///     again inside the queue immediately before <see cref="Execute"/>,
    ///     against the state the action is about to act on. A refusal there
    ///     skips the action and completes the job rather than failing it.
    ///   </para>
    ///   <para>
    ///     Write it so it can be asked twice: cheap, free of side effects, and
    ///     answering about the present rather than about when it was queued. A
    ///     queue can be hours deep, so the second answer is the one that
    ///     decides.
    ///   </para>
    /// </remarks>
    /// <param name="token">
    ///   Bound to the current API request on the first call, and to the worker
    ///   pool on the second.
    /// </param>
    /// <returns>
    ///   A rejection reason, or <see langword="null"/> to allow the
    ///   invocation.
    /// </returns>
    Task<ActionValidationResult?> Validate(CancellationToken token = default)
        => Task.FromResult<ActionValidationResult?>(null);

    /// <summary>
    ///   Execute the action.
    /// </summary>
    /// <remarks>
    ///   The action instance is resolved fresh from DI (transient) for every
    ///   execution; statefulness is the implementation's own responsibility.
    ///   Exceptions are caught by the worker and logged as a queue job
    ///   failure. There is no result-reporting hook — actions that want to
    ///   report something log it, same as the rest of the queue already does.
    /// </remarks>
    /// <param name="token">
    ///   The cancellation token of the queue worker running the action, not of
    ///   the invoking request — there is no live HTTP request left by the time a
    ///   queued action runs. It is cancelled when the worker pool is stopped
    ///   (server shutdown, or an explicit queue stop) and at no other time:
    ///   there is no way to cancel a single running action. Long-running actions
    ///   should still honour it so shutdown is not held up.
    /// </param>
    Task Execute(CancellationToken token = default);
}
