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
    string? Description => null;

    /// <summary>
    ///   The category of the action. Defaults to
    ///   <see cref="ActionCategory.Miscellaneous"/>, the shared fallback
    ///   category for any action that declares none.
    /// </summary>
    ActionCategory Category => ActionCategory.Miscellaneous;

    /// <summary>
    ///   The permission required to invoke the action.
    /// </summary>
    /// <remarks>
    ///   There is deliberately no default implementation — every action must
    ///   state its permission explicitly. <see cref="ActionService.AddParts"/>
    ///   rejects any action type that does not declare it on the type itself.
    /// </remarks>
    ActionPermission Permission { get; }

    /// <summary>
    ///   UI hint for destructive actions. The server does not enforce a
    ///   confirmation step; the WebUI is expected to prompt before invoking
    ///   the action when this is <see langword="true"/>.
    /// </summary>
    bool RequiresConfirmation => false;

    /// <summary>
    ///   Optional synchronous pre-check, run by the API before the action is
    ///   enqueued. Return a non-null result to reject the invocation
    ///   immediately (e.g. HTTP 400) without ever touching the queue.
    ///   Default: always allowed.
    /// </summary>
    /// <param name="token">
    ///   The cancellation token bound to the current API request.
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
    ///   report something log, same as the rest of the queue already does.
    /// </remarks>
    /// <param name="token">
    ///   The cancellation token, bound to the queue job lifecycle rather than
    ///   the invoking request — there is no live HTTP request left by the
    ///   time a queued action runs.
    /// </param>
    Task Execute(CancellationToken token = default);
}
