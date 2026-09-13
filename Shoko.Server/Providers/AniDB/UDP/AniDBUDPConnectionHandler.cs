using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Connectivity.Enums;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.Abstractions.Metadata.Anidb.Events;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Connection;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Server;
using Shoko.Server.Settings;

using Timer = System.Timers.Timer;

namespace Shoko.Server.Providers.AniDB.UDP;

public partial class AniDBUDPConnectionHandler : ConnectionHandler, IUDPConnectionHandler, IUdpSessionTransport, IAniDbUdpRequestChannel
{
    /****
    * From Anidb wiki:
    * The virtual UDP connection times out if no data was received from the client for 35 minutes.
    * A client should issue a UPTIME command once every 30 minutes to keep the connection alive should that be required.
    * If the client does not use any of the notification/push features of the API it should NOT keep the connection alive, furthermore it should explicitly terminate the connection by issuing a LOGOUT command once it finished it's work.
    * If it is very likely that another command will be issued shortly (within the next 20 minutes) a client SHOULD keep the current connection open, by not sending a LOGOUT command.
    ****/
    // 5 minutes
    private const int LogoutPeriod = 5 * 60 * 1000;
    private readonly IRequestFactory _requestFactory;
    private readonly UDPRateLimiter _rateLimiter;
    private readonly IConnectivityService _connectivityService;
    private readonly IAniDBSocketHandlerFactory _socketHandlerFactory;
    private IAniDBSocketHandler? _socketHandler;
    private readonly SemaphoreSlim _socketLock = new(1, 1);
    private readonly AniDbUdpSession _session;

    // _socketLock is a non-reentrant SemaphoreSlim, but the call graph nests
    // (SendAsync -> LoginAsync -> RequestLogin -> SendDirectlyAsync, and the same
    // for ForceLogout/ForceReconnection/Init). This per-execution depth counter
    // makes it reentrant: only the outermost acquisition waits and only the
    // innermost release frees the lock. The increment and the matching decrement
    // must live in the same async state machine: an AsyncLocal write does not
    // flow back to a caller awaiting this method (ExecutionContext is one-way),
    // so splitting the two across methods loses the depth on release and leaks
    // the lock.
    private static readonly AsyncLocal<int> s_socketLockDepth = new();

    // The timer callbacks fire-and-forget these tasks. Holding the Task in a
    // field keeps the state machine reachable, so a ping that is holding the
    // socket lock can never be garbage collected mid-flight and strand the
    // lock.
    private Task? _inFlightPing;
    private Task? _inFlightLogout;

    // IDK Rider said to use a GeneratedRegex attribute
    private static readonly Regex s_logMask = GetLogRegex();

    [GeneratedRegex("(?<=(\\bpass=|&pass=\\bs=|&s=))[^&]+", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex GetLogRegex();

    public override string Type => "UDP";
    protected override UpdateType BanEnum => UpdateType.UDPBan;

    public string? SessionID => _session.SessionID;

    public bool IsAlive { get; private set; }

    public string ImageServerUrl => string.Format(Constants.URLS.AniDB_Images, _session.CdnDomain);

    private readonly ISettingsProvider _settingsProvider;

    private Timer? _pingTimer;
    private Timer? _logoutTimer;

    public bool IsInvalidSession
    {
        get => _session.IsInvalidSession;
        set => _session.IsInvalidSession = value;
    }

    public bool IsLoginFailed => _session.IsLoginFailed;

    public bool IsNetworkAvailable { private set; get; }

    public AniDBUDPConnectionHandler(IRequestFactory requestFactory, ILoggerFactory loggerFactory, ISettingsProvider settings, UDPRateLimiter rateLimiter, IConnectivityService connectivityService, AniDbBanStateService banStateService, IAniDBSocketHandlerFactory socketHandlerFactory) :
        base(loggerFactory, banStateService.Udp)
    {
        _socketHandlerFactory = socketHandlerFactory;
        _requestFactory = requestFactory;
        _rateLimiter = rateLimiter;
        _connectivityService = connectivityService;
        _settingsProvider = settings;
        _session = new(this, requestFactory, settings, loggerFactory.CreateLogger<AniDbUdpSession>());
        // A ban change resets the session, however it is triggered (a BANNED
        // response, the zero-byte heuristic, or the ban expiring on its own).
        BanState.BanOccurred += OnBanStateBanned;
        BanState.BanExpired += OnBanStateUnbanned;
    }

    ~AniDBUDPConnectionHandler()
    {
        Logger.LogInformation("Disposing AniDBUDPConnectionHandler...");
        IsNetworkAvailable = false;
        IsAlive = false;
        BanState.BanOccurred -= OnBanStateBanned;
        BanState.BanExpired -= OnBanStateUnbanned;
        _pingTimer?.Stop();
        _pingTimer?.Dispose();
        _pingTimer = null;
        _logoutTimer?.Stop();
        _logoutTimer?.Dispose();
        _logoutTimer = null;
        _socketHandler?.Dispose();
        _socketHandler = null;
    }

    private void OnBanStateBanned(object? sender, AnidbBanOccurredEventArgs e) => _session.HandleBan(true);

    private void OnBanStateUnbanned(object? sender, AnidbBanOccurredEventArgs e) => _session.HandleBan(false);

    public new void StartBackoffTimer(int secsToPause, string pauseReason)
    {
        base.StartBackoffTimer(secsToPause, pauseReason);
    }

    bool IUdpSessionTransport.IsBanned => IsBanned;

    Task IUdpSessionTransport.ForceReconnectionAsync(CancellationToken cancellationToken) => ForceReconnectionAsync(cancellationToken);

    void IUdpSessionTransport.UpdateState(AniDBStateUpdate update) => UpdateState(update);

    void IUdpSessionTransport.StopPinging() => StopPinging();

    public async Task<bool> InitAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settingsProvider.GetSettings();
        if (!AniDbUdpSession.ValidAniDBCredentials(settings.AniDb.Username, settings.AniDb.Password)) return false;
        await InitInternalAsync(cancellationToken);
        return true;
    }

    public async Task<bool> InitAsync(string? username, string? password, string serverName, ushort serverPort, ushort clientPort, CancellationToken cancellationToken = default)
    {
        var settings = _settingsProvider.GetSettings();
        settings.AniDb.UDPServerAddress = serverName;
        settings.AniDb.UDPServerPort = serverPort;
        settings.AniDb.ClientPort = clientPort;

        if (!AniDbUdpSession.ValidAniDBCredentials(username, password)) return false;

        _session.SetCredentials(username, password);
        await InitInternalAsync(cancellationToken);
        return true;
    }

    private async Task InitInternalAsync(CancellationToken cancellationToken)
    {
        var settings = _settingsProvider.GetSettings();
        ArgumentNullException.ThrowIfNull(settings.AniDb?.UDPServerAddress);
        if (settings.AniDb.UDPServerPort == 0) throw new ArgumentException("AniDB UDP Server Port is invalid");
        if (settings.AniDb.ClientPort == 0) throw new ArgumentException("AniDB Client Port is invalid");

        await WithSocketLockAsync(async () =>
        {
            if (_socketHandler != null)
            {
                await _socketHandler.DisposeAsync();
                _socketHandler = null;
            }

            _socketHandler = _socketHandlerFactory.Create(settings.AniDb.UDPServerAddress, settings.AniDb.UDPServerPort, settings.AniDb.ClientPort);
            IsNetworkAvailable = await _socketHandler.TryConnectionAsync(cancellationToken);
        }, cancellationToken);

        _session.ResetState();

        _pingTimer = new Timer { Interval = settings.AniDb.UDPPingFrequency * 1000, Enabled = true, AutoReset = true };
        _pingTimer.Elapsed += PingTimerElapsed;
        _logoutTimer = new Timer { Interval = LogoutPeriod, Enabled = true, AutoReset = false };
        _logoutTimer.Elapsed += LogoutTimerElapsed;

        IsAlive = true;
    }

    private void PingTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        // One ping at a time. Holding the Task in a field keeps the state
        // machine rooted (see _inFlightPing).
        if (_inFlightPing is { IsCompleted: false }) return;

        // The timer's execution context may have been captured while the socket
        // lock was held, so start from a clean reentrancy depth.
        s_socketLockDepth.Value = 0;
        _inFlightPing = Task.Run(() => PingAsync());
    }

    private async Task PingAsync()
    {
        try
        {
            if (!_session.IsLoggedOn) return;

            bool connected = false;
            await WithSocketLockAsync(async () =>
            {
                connected = _socketHandler is { IsConnected: true };
            }, CancellationToken.None);
            if (!connected) return;

            if (IsBanned || BackoffSecs.HasValue) return;

            var ping = _requestFactory.Create<RequestPing>();
            await ping.SendAsync();
        }
        catch (UnexpectedUDPResponseException)
        {
            _pingTimer?.Stop();
        }
        catch (AniDBBannedException)
        {
            _pingTimer?.Stop();
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "{Message}", exception);
        }
    }

    private void LogoutTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_inFlightLogout is { IsCompleted: false }) return;

        s_socketLockDepth.Value = 0;
        _inFlightLogout = Task.Run(() => LogoutAsync());
    }

    private async Task LogoutAsync()
    {
        try
        {
            if (!_session.IsLoggedOn) return;

            bool connected = false;
            await WithSocketLockAsync(async () =>
            {
                connected = _socketHandler is { IsConnected: true };
            }, CancellationToken.None);
            if (!connected) return;

            if (IsBanned || BackoffSecs.HasValue) return;

            await ForceLogoutAsync();
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "{Message}", exception);
        }
    }

    /// <summary>
    /// Actually get data from AniDB
    /// </summary>
    /// <param name="command">The request to be made (AUTH user=baka&amp;pass....)</param>
    /// <param name="needsUnicode">Only for Login, specify whether to ask for UTF16</param>
    /// <param name="cancellationToken"></param>
    public async Task<string> SendAsync(string command, bool needsUnicode = true, CancellationToken cancellationToken = default)
    {
        // Steps:
        // 1. Check Ban state and throw if Banned
        // 2. Check Login State and Login if needed
        // 3. Actually Call AniDB

        // Check Ban State
        // Ideally, this will never happen, as we stop the queue and attempt a graceful rollback of the command
        if (IsBanned)
        {
            throw AniDBBannedException.For(BanState);
        }
        // TODO Low Priority: We need to handle Login Attempt Decay, so that we can try again if it's not just a bad user/pass
        // It wasn't handled before, and it's not caused serious problems

        // login doesn't use this method, so this check won't interfere with it
        // if we got here, and it's invalid session, then it already failed to re-log
        if (_session.IsInvalidSession)
        {
            throw new NotLoggedInException();
        }

        string response = null!;
        await WithSocketLockAsync(async () =>
        {
            // Check Login State
            if (!await LoginAsync(cancellationToken))
            {
                throw new LoginFailedException();
            }

            // Actually Call AniDB
            response = await SendInternalAsync(command, needsUnicode, cancellationToken: cancellationToken);
        }, cancellationToken);
        return response;
    }

    public async Task<string> SendDirectlyAsync(string command, bool needsUnicode = true, bool isPing = false, bool isLogout = false, CancellationToken cancellationToken = default)
    {
        string response = null!;
        await WithSocketLockAsync(async () =>
        {
            response = await SendInternalAsync(command, needsUnicode, isPing, isLogout, cancellationToken);
        }, cancellationToken);
        return response;
    }

    private async Task<string> SendInternalAsync(string command, bool needsUnicode = true, bool isPing = false, bool isLogout = false, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_socketHandler is not { IsConnected: true }, "The connection was closed by shoko");
        var socketHandler = _socketHandler;

        try
        {
            // we want to reset the logout timer anyway
            if (!isLogout && !isPing)
            {
                _pingTimer?.Stop();
                _logoutTimer?.Stop();
            }

            // 1. Call AniDB
            // 2. Decode the response, converting Unicode and decompressing, as needed
            // 3. Check for an Error Response
            // 4. Return a pretty response object, with a parsed return code and trimmed string
            var encoding = needsUnicode ? new UnicodeEncoding(true, false) : Encoding.ASCII;
            var sendBytes = encoding.GetBytes(command);

            try
            {
                return await SendCommandAsync(socketHandler, sendBytes, needsUnicode, isPing, command, cancellationToken);
            }
            catch (Exception exception) when (IsTimeout(exception, cancellationToken))
            {
                Logger.LogWarning("AniDB request timed out. Checking Network and trying again");
                await _connectivityService.CheckAvailability();
                return await SendCommandAsync(socketHandler, sendBytes, needsUnicode, isPing, command, cancellationToken);
            }
        }
        finally
        {
            if (!isLogout && !isPing)
            {
                _pingTimer?.Start();
                _logoutTimer?.Start();
            }
        }
    }

    private async Task<string> SendCommandAsync(IAniDBSocketHandler socketHandler, byte[] sendBytes, bool needsUnicode, bool isPing, string command, CancellationToken cancellationToken)
    {
        return await _rateLimiter.EnsureRate(async () =>
        {
            if (_connectivityService.NetworkAvailability < NetworkAvailability.PartialInternet)
            {
                Logger.LogError("No internet, so not sending AniDB request");
                throw new SocketException((int)SocketError.HostUnreachable);
            }

            var start = DateTime.Now;
            Logger.LogTrace("AniDB UDP Call: (Using {Unicode}) {Command}", needsUnicode ? "Unicode" : "ASCII", MaskLog(command));
            var bytesReceived = await socketHandler.SendAsync(sendBytes, cancellationToken);

            if (bytesReceived.All(a => a == 0))
            {
                // we are probably banned or have lost connection. We can't tell the difference, so we're assuming ban
                throw AniDBBannedException.For(BanState);
            }

            // decode
            var decodedString = AniDBSocketHandler.GetEncoding(bytesReceived).GetString(bytesReceived, 0, bytesReceived.Length);
            // remove BOM
            if (decodedString[0] == 0xFEFF) decodedString = decodedString[1..];

            var ts = DateTime.Now - start;
            Logger.LogTrace("AniDB Response: Received in {Time:ss'.'ffff}s\n{DecodedString}", ts, MaskLog(decodedString));
            return decodedString;
        }, forceShortDelay: isPing);
    }

    private void StopPinging()
    {
        _pingTimer?.Stop();
        _logoutTimer?.Stop();
    }

    public async Task ForceReconnectionAsync(CancellationToken cancellationToken = default)
    {
        await WithSocketLockAsync(async () =>
        {
            try
            {
                await ForceLogoutCoreAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to logout");
            }

            try
            {
                await CloseConnectionsCoreAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to close socket");
            }

            try
            {
                await InitInternalAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to reinitialize socket");
            }
        }, cancellationToken);
    }

    public async Task ForceLogoutAsync(CancellationToken cancellationToken = default)
        => await WithSocketLockAsync(() => ForceLogoutCoreAsync(cancellationToken), cancellationToken);

    private Task ForceLogoutCoreAsync(CancellationToken cancellationToken)
        => _session.ForceLogoutCoreAsync(cancellationToken);

    public void ClearSession()
    {
        StopPinging();
        _session.ClearSessionState();
    }

    public async Task CloseConnectionsAsync(CancellationToken cancellationToken = default)
        => await WithSocketLockAsync(() => CloseConnectionsCoreAsync(), cancellationToken);

    private async Task CloseConnectionsCoreAsync()
    {
        IsNetworkAvailable = false;
        IsAlive = false;

        _pingTimer?.Stop();
        _pingTimer?.Dispose();
        _pingTimer = null;

        _logoutTimer?.Stop();
        _logoutTimer?.Dispose();
        _logoutTimer = null;

        if (_socketHandler == null) return;
        Logger.LogInformation("AniDB UDP Socket Disposing...");
        await _socketHandler.DisposeAsync();
        _socketHandler = null;
    }

    public async Task<bool> LoginAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settingsProvider.GetSettings();
        var loggedIn = false;
        await WithSocketLockAsync(async () =>
        {
            loggedIn = await _session.LoginAsync(settings.AniDb.Username, settings.AniDb.Password, cancellationToken);
        }, cancellationToken);
        return loggedIn;
    }

    public async Task<bool> TestLoginAsync(string? username, string? password, CancellationToken cancellationToken = default)
    {
        if (!AniDbUdpSession.ValidAniDBCredentials(username, password))
        {
            return false;
        }

        var result = false;
        await WithSocketLockAsync(async () =>
        {
            result = await _session.LoginCoreAsync(username, password, cancellationToken);
            if (result)
            {
                await _session.ForceLogoutCoreAsync(cancellationToken);
            }
        }, cancellationToken);
        return result;
    }

    public bool SetCredentials(string username, string password) => _session.SetCredentials(username, password);

    public bool ValidAniDBCredentials([NotNullWhen(true)] string? user, [NotNullWhen(true)] string? pass)
        => AniDbUdpSession.ValidAniDBCredentials(user, pass);

    private async Task WithSocketLockAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        var depth = s_socketLockDepth.Value;
        s_socketLockDepth.Value = depth + 1;

        // A cancellation while waiting throws before the semaphore is granted,
        // so track whether it actually was, to avoid releasing an unheld lock.
        var acquired = false;
        if (depth == 0)
        {
            await _socketLock.WaitAsync(cancellationToken);
            acquired = true;
        }

        try
        {
            await action();
        }
        finally
        {
            // The decrement must read the depth from this same state machine:
            // the increment above is part of this execution, but it is not
            // visible to a caller awaiting this method.
            var currentDepth = s_socketLockDepth.Value;
            s_socketLockDepth.Value = currentDepth - 1;
            if (currentDepth == 1 && acquired)
                _socketLock.Release();
        }
    }

    private static bool IsTimeout(Exception exception, CancellationToken cancellationToken)
        => exception is SocketException { SocketErrorCode: SocketError.TimedOut }
           || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    private static string MaskLog(string input)
    {
        return s_logMask.Replace(input, "****");
    }
}
