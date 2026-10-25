using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;

namespace Shoko.Abstractions.ScheduledActions;

/// <summary>
///   Hands the core scheduled actions that are not classes of their own, such
///   as one per schedule an admin set up in a plugin's settings, and which may
///   come and go while the server runs. Core or a plugin registers one by
///   exporting a class implementing this interface.
/// </summary>
/// <remarks>
///   Each action is known by the key its <see cref="ScheduledActionDescriptor"/>
///   gives. Its ID is a UUIDv5 of <c>"ScheduledActionProvider={provider class's full name},Action={key}"</c>
///   under the plugin's ID, so keep both stable: renaming or moving the class,
///   or changing a key, loses the stored triggers and last runs. Removing an
///   action from the list removes its triggers and last runs too; while the
///   plugin is only disabled or unloaded, they are kept for its return.
///   Everything else about a run is as for an <see cref="IScheduledAction"/>.
/// </remarks>
public interface IScheduledActionProvider
{
    /// <summary>
    ///   Raised when actions were added, removed or changed, once
    ///   <see cref="GetActions"/> answers with the new list. The core reads the
    ///   whole list again and goes by it.
    /// </summary>
    event EventHandler? ActionsChanged;

    /// <summary>
    ///   Lists the actions there are now. Read at start-up, before the
    ///   scheduling starts, and again after every <see cref="ActionsChanged"/>.
    /// </summary>
    /// <returns>The actions, each key once.</returns>
    IReadOnlyList<ScheduledActionDescriptor> GetActions();

    /// <summary>
    ///   Optional. Says whether an action can run now, as
    ///   <see cref="IScheduledAction.Validate"/> does.
    /// </summary>
    /// <param name="key">The action's key.</param>
    /// <param name="token">Cancels the check.</param>
    /// <returns><c>null</c> when it can run, or why it cannot.</returns>
    Task<ActionValidationResult?> Validate(string key, CancellationToken token)
        => Task.FromResult<ActionValidationResult?>(null);

    /// <summary>
    ///   Runs an action, as <see cref="IScheduledAction.Execute"/> does. A run
    ///   already going when the action is removed is let finish.
    /// </summary>
    /// <param name="key">The action's key.</param>
    /// <param name="progress">Takes the run's progress, from 0 to 100.</param>
    /// <param name="token">Cancels the run.</param>
    /// <returns>A task that completes when the run ends.</returns>
    Task Execute(string key, IProgress<decimal> progress, CancellationToken token);
}
