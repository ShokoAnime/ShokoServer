using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Shoko.Abstractions.Core.Events;
using Shoko.Abstractions.Core.Exceptions;

namespace Shoko.Abstractions.Core.Services;

/// <summary>
///   System service. Contains information about the state of the server, and
///   provides methods for stopping and restarting the server.
/// </summary>
public interface ISystemService
{
    #region General

    /// <summary>
    ///   The time the server was initially bootstrapped.
    /// </summary>
    DateTime BootstrappedAt { get; }

    /// <summary>
    ///   The uptime of the server.
    /// </summary>
    TimeSpan Uptime => DateTime.UtcNow - BootstrappedAt;

    /// <summary>
    ///   The time it took to start the server.
    /// </summary>
    TimeSpan? StartupTime => StartedAt.HasValue ? StartedAt.Value - BootstrappedAt : null;

    /// <summary>
    ///   The version of the currently running server.
    /// </summary>
    VersionInformation Version { get; }

    /// <summary>
    ///   The version of the MediaInfo executable we use, if available.
    /// </summary>
    string? MediaInfoVersion { get; }

    /// <summary>
    ///   The version of the RHash library we use, if available.
    /// </summary>
    string? RHashVersion { get; }

    #endregion

    #region Startup

    /// <summary>
    ///   Dispatched when the server has changed the startup message.
    /// </summary>
    event EventHandler<StartupMessageChangedEventArgs>? StartupMessageChanged;

    /// <summary>
    ///   Dispatched when the server has failed to start.
    /// </summary>
    event EventHandler<StartupFailedEventArgs>? StartupFailed;

    /// <summary>
    ///  Dispatched right before the server is fully started, so plugins can
    ///  initialize their services which relies on database being fully
    ///  initialized and system services being ready.
    /// </summary>
    event EventHandler<ServerAboutToStartEventArgs>? AboutToStart;

    /// <summary>
    ///   Dispatched when the server has fully started and all services are
    ///   usable.
    /// </summary>
    event EventHandler? Started;

    /// <summary>
    ///   Indicates that the server has fully started and all core services are
    ///   usable.
    /// </summary>
    bool IsStarted { get; }

    /// <summary>
    ///   The time the server was fully started after the initial bootstrapping.
    /// </summary>
    DateTime? StartedAt { get; }

    /// <summary>
    ///   The current message that is displayed to the user during startup.
    /// </summary>
    string? StartupMessage { get; }

    /// <summary>
    ///  The exception that was thrown during startup, if any.
    /// </summary>
    StartupFailedException? StartupFailedException { get; }

    /// <summary>
    ///   Starts the server.
    /// </summary>
    /// <returns>
    ///   The <see cref="IHost"/> instance if the server was started
    ///   successfully, otherwise <see langword="null" />.
    /// </returns>
    Task<IHost?> StartAsync();

    /// <summary>
    ///  Waits for the server to be fully started.
    /// </summary>
    /// <exception cref="StartupFailedException">Thrown if the server failed to start.</exception>
    Task WaitForStartupAsync();

    #endregion

    #region Setup

    /// <summary>
    ///   Dispatched when the server is put into setup mode, and waiting for the
    ///   user to configure any settings before manually starting the server.
    /// </summary>
    event EventHandler? SetupRequired;

    /// <summary>
    ///   Dispatched when the server has completed the setup process when it
    ///   was in setup mode.
    /// </summary>
    event EventHandler? SetupCompleted;

    /// <summary>
    ///   Indicates that the server is in setup mode, and waiting for the user
    ///   to configure any settings before manually starting the server.
    /// </summary>
    bool InSetupMode { get; }

    /// <summary>
    ///   Indicates to the server that the setup process has been completed, and
    ///   it can continue with the startup process.
    /// </summary>
    /// <returns>
    ///   <see langword="true" /> if the server was in setup mode and the setup
    ///   process was completed, otherwise <see langword="false" />.
    /// </returns>
    bool CompleteSetup();

    #endregion

    #region Shutdown

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
    ///   Indicates that we can perform a controlled shutdown.
    /// </summary>
    bool CanShutdown { get; }

    /// <summary>
    ///   Indicates that a shutdown is pending.
    /// </summary>
    bool ShutdownPending { get; }

    /// <summary>
    ///   Request a shutdown of the server.
    /// </summary>
    /// <returns>
    ///   <see langword="true" /> if the shutdown was permitted, otherwise
    ///   <see langword="false" />.
    /// </returns>
    bool RequestShutdown();

    /// <summary>
    ///   Wait for the server to shutdown.
    /// </summary>
    Task WaitForShutdownAsync();

    #region Shutdown | Restart

    /// <summary>
    ///   Indicates that we can perform a controlled restart.
    /// </summary>
    bool CanRestart { get; }

    /// <summary>
    ///   Indicates that a restart is pending.
    /// </summary>
    bool RestartPending { get; }

    /// <summary>
    ///   Request a restart of the server.
    /// </summary>
    /// <returns>
    ///   <see langword="true" /> if the restart was permitted, otherwise
    ///   <see langword="false" />.
    /// </returns>
    bool RequestRestart();

    #endregion

    #endregion

    #region Database

    /// <summary>
    ///   Dispatched when the database is blocked or unblocked.
    /// </summary>
    event EventHandler<DatabaseBlockedChangedEventArgs>? DatabaseBlockedChanged;

    /// <summary>
    ///   Indicates that database access may be blocked, and that we should
    ///   wait for the database to be unblocked before attempting to access it.
    /// </summary>
    bool IsDatabaseBlocked { get; }

    /// <summary>
    ///   Wait for the database to be unblocked.
    /// </summary>
    Task WaitForDatabaseUnblockedAsync();

    #endregion
}
