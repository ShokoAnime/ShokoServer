using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Shoko.Abstractions.Core.Events;

namespace Shoko.Abstractions.Core;

/// <summary>
///   System service. Contains information about the state of the server, and
///   provides methods for stopping and restarting the server.
/// </summary>
public interface ISystemService
{
    /// <summary>
    ///  Dispatched right before the server is fully started, so plugins can
    ///  initialize their services which relies on database being fully
    ///  initialized and system services being ready.
    /// </summary>
    event EventHandler<ServerAboutToStartEventArgs>? AboutToStart;

    /// <summary>
    ///   Dispatched when the the server has fully started and all services are
    ///   usable.
    /// </summary>
    event EventHandler? Started;

    /// <summary>
    ///   Dispatched when the a shutdown or restart has been requested, and the
    ///   server is about to shut down. Event subscribers can cancel the
    ///   shutdown by setting the <see cref="CancelEventArgs.Cancel"/> property
    ///   to <see langword="true" />.
    /// </summary>
    event EventHandler<CancelEventArgs>? ShutdownOrRestartRequested;

    /// <summary>
    ///   Dispatched when the server is about to shut down. We're on a tight
    ///   time budget, as the parent process may kill the server if it doesn't
    ///   shut down in "a timely manner." Use this event to perform any last
    ///   minute cleanup before the server shuts down.
    /// </summary>
    event EventHandler? Shutdown;

    /// <summary>
    ///   Indicates that the server has fully started and all core services are
    ///   usable.
    /// </summary>
    bool IsStarted { get => StartedAt.HasValue; }

    /// <summary>
    ///   Indicates that we can perform a controlled shutdown.
    /// </summary>
    bool CanShutdown { get; }

    /// <summary>
    ///   Indicates that we can perform a controlled restart.
    /// </summary>
    bool CanRestart { get; }

    /// <summary>
    ///   Indicates that a shutdown is pending.
    /// </summary>
    bool ShutdownPending { get; }

    /// <summary>
    ///   Indicates that a restart is pending.
    /// </summary>
    bool RestartPending { get; }

    /// <summary>
    ///   The version of the currently running server.
    /// </summary>
    VersionInformation Version { get; }

    /// <summary>
    ///   The time the server was initially bootstrapped.
    /// </summary>
    DateTime BootstrappedAt { get; }

    /// <summary>
    ///   The time the server was fully started after the initial bootstrapping.
    /// </summary>
    DateTime? StartedAt { get; }

    /// <summary>
    ///   Request a shutdown of the server.
    /// </summary>
    /// <returns>
    ///   <see langword="true" /> if the shutdown was permitted, otherwise
    ///   <see langword="false" />.
    /// </returns>
    bool RequestShutdown();

    /// <summary>
    ///   Request a restart of the server.
    /// </summary>
    /// <returns>
    ///   <see langword="true" /> if the restart was permitted, otherwise
    ///   <see langword="false" />.
    /// </returns>
    bool RequestRestart();
}
