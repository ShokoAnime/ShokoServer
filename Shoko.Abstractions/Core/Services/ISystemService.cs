using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Shoko.Abstractions.Core.Events;
using Shoko.Abstractions.Core.Exceptions;
using Shoko.Abstractions.Plugin;

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
    TimeSpan Uptime { get => DateTime.UtcNow - BootstrappedAt; }

    /// <summary>
    ///   The time it took to start the server.
    /// </summary>
    TimeSpan? StartupTime { get => StartedAt.HasValue ? StartedAt.Value - BootstrappedAt : null; }

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
    ///   successfully, otherwise <c>null</c>.
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
    ///   <c>true</c> if the server was in setup mode and the setup
    ///   process was completed, otherwise <c>false</c>.
    /// </returns>
    bool CompleteSetup();

    #endregion

    #region Shutdown

    /// <summary>
    ///   Dispatched when the a shutdown or restart has been requested, and the
    ///   server is about to shut down. Event subscribers can cancel the
    ///   shutdown by setting the <see cref="CancelEventArgs.Cancel"/> property
    ///   to <c>true</c>.
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
    ///   <c>true</c> if the shutdown was permitted, otherwise
    ///   <c>false</c>.
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
    ///   Indicates that a restart has been requested and is under way. Whether
    ///   a change is waiting on a restart is <see cref="RestartRequired"/>.
    /// </summary>
    bool RestartPending { get; }

    /// <summary>
    ///   Request a restart of the server.
    /// </summary>
    /// <returns>
    ///   <c>true</c> if the restart was permitted, otherwise
    ///   <c>false</c>.
    /// </returns>
    bool RequestRestart();

    #endregion

    #region Shutdown | Restart Required

    /// <summary>
    ///   Dispatched whenever a restart reason is added or cleared,
    ///   with every reason that stands afterwards. Raised synchronously, so
    ///   keep handlers short. A handler that throws is logged and does not
    ///   stop the others, and a change a handler makes is sent after them.
    /// </summary>
    event EventHandler<RestartReasonsChangedEventArgs>? RestartReasonsChanged;

    /// <summary>
    ///   Every reason the server needs a restart for a change to take effect:
    ///   one while any configuration has changed restart-only members, one
    ///   while any plugin's state changes on restart, and the reasons plugins
    ///   raised themselves. Empty after a restart, since the reasons live in
    ///   memory.
    /// </summary>
    IReadOnlyList<RestartReason> RestartReasons { get; }

    /// <summary>
    ///   Indicates that at least one of the <see cref="RestartReasons"/>
    ///   stands, so a change is waiting on a restart. Unlike
    ///   <see cref="RestartPending"/>, it does not mean a restart was asked for.
    /// </summary>
    bool RestartRequired { get => RestartReasons.Count > 0; }

    /// <summary>
    ///   Raise a restart reason owned by a plugin, for a change of its own
    ///   that only applies after a restart. Dispose the returned handle once
    ///   the reason is gone.
    /// </summary>
    /// <typeparam name="TPlugin">
    ///   The plugin's <see cref="IPlugin"/> type, which must be an active
    ///   plugin.
    /// </typeparam>
    /// <param name="description">
    ///   A short, human-readable description of the change.
    /// </param>
    /// <returns>
    ///   The hold on the reason. The reason stands until the handle is
    ///   disposed or the server restarts. Each call raises a reason of its own.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///   <paramref name="description"/> is empty.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   <typeparamref name="TPlugin"/> is not an active plugin.
    /// </exception>
    IRestartRequirement RequireRestart<TPlugin>(string description) where TPlugin : class, IPlugin;

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

    #region Services

    private static IServiceProvider? _services;

    /// <summary>
    ///   Determines if the static services are available yet.
    /// </summary>
    public static bool HasStaticServices { get => _services is not null; }

    /// <summary>
    ///   Get or set the static service provider. DO NOT USE UNLESS ABSOLUTELY
    ///   NECESSARY.
    /// </summary>
    /// <remarks>
    ///   Will be set during startup. DO NOT SET MANUALLY.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///   The service provider has already been set.
    /// </exception>
    [Obsolete("Do not use this unless DI is not an option and only use it as a LAST RESORT!")]
    public static IServiceProvider StaticServices
    {
        get => _services ?? throw new InvalidOperationException("The service provider has not been set.");
        set => _services = _services is null ? value : throw new InvalidOperationException("The service provider has already been set.");
    }

    #endregion
}
