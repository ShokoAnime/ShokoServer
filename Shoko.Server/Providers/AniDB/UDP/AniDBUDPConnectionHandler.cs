using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using Microsoft.Extensions.Logging;
using Polly;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Connection;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Timer = System.Timers.Timer;

namespace Shoko.Server.Providers.AniDB.UDP;

#nullable enable
public partial class AniDBUDPConnectionHandler : ConnectionHandler, IUDPConnectionHandler
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
    private AniDBSocketHandler? _socketHandler;
    private readonly object _socketHandlerLock = new();
    // IDK Rider said to use a GeneratedRegex attribute
    private static readonly Regex s_logMask = GetLogRegex();
    
    [GeneratedRegex("(?<=(\\bpass=|&pass=\\bs=|&s=))[^&]+", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex GetLogRegex();

    public event EventHandler? LoginFailed;

    public override double BanTimerResetLength => 1.5D;
    public override string Type => "UDP";
    protected override UpdateType BanEnum => UpdateType.UDPBan;

    public string? SessionID { get; private set; }
    public bool IsAlive { get; private set; }

    private string _cdnDomain = Constants.URLS.AniDB_Images_Domain;

    public string ImageServerUrl => string.Format(Constants.URLS.AniDB_Images, _cdnDomain);

    private ISettingsProvider SettingsProvider { get; set; }

    private Timer? _pingTimer;
    private Timer? _logoutTimer;

    private bool _isLoggedOn;
    private bool _isInvalidSession;

    public bool IsInvalidSession
    {
        get => _isInvalidSession;

        set
        {
            _isInvalidSession = value;
            UpdateState(new AniDBStateUpdate
            {
                UpdateType = UpdateType.InvalidSession,
                UpdateTime = DateTime.Now,
                Value = value
            });
        }
    }

    public override bool IsBanned
    {
        get => base.IsBanned;
        set
        {
            if (value)
            {
                _isLoggedOn = false;
                SessionID = null;
            }

            IsInvalidSession = false;

            base.IsBanned = value;
        }
    }

    public bool IsNetworkAvailable { private set; get; }

    public AniDBUDPConnectionHandler(IRequestFactory requestFactory, ILoggerFactory loggerFactory, ISettingsProvider settings, UDPRateLimiter rateLimiter, IConnectivityService connectivityService) :
        base(loggerFactory)
    {
        _requestFactory = requestFactory;
        _rateLimiter = rateLimiter;
        _connectivityService = connectivityService;
        SettingsProvider = settings;
    }

    ~AniDBUDPConnectionHandler()
    {
        Logger.LogInformation("Disposing AniDBUDPConnectionHandler...");
        CloseConnections();
    }

    void IUDPConnectionHandler.StartBackoffTimer(int time, string message)
    {
        base.StartBackoffTimer(time, message);
    }

    public bool Init()
    {
        var settings = SettingsProvider.GetSettings();
        if (!ValidAniDBCredentials(settings.AniDb.Username, settings.AniDb.Password)) return false;
        InitInternal();
        return true;
    }

    public bool Init(string username, string password, string serverName, ushort serverPort, ushort clientPort)
    {
        var settings = SettingsProvider.GetSettings();
        settings.AniDb.UDPServerAddress = serverName;
        settings.AniDb.UDPServerPort = serverPort;
        settings.AniDb.ClientPort = clientPort;

        if (!ValidAniDBCredentials(username, password)) return false;

        SetCredentials(username, password);
        InitInternal();
        return true;
    }

    private void InitInternal()
    {
        var settings = SettingsProvider.GetSettings();
        ArgumentNullException.ThrowIfNull(settings.AniDb?.UDPServerAddress);
        if (settings.AniDb.UDPServerPort == 0) throw new ArgumentException("AniDB UDP Server Port is invalid");
        if (settings.AniDb.ClientPort == 0) throw new ArgumentException("AniDB Client Port is invalid");

        lock(_socketHandlerLock)
        {
            if (_socketHandler != null)
            {
                _socketHandler.Dispose();
                _socketHandler = null;
            }

            _socketHandler = new AniDBSocketHandler(_loggerFactory, settings.AniDb.UDPServerAddress, settings.AniDb.UDPServerPort, settings.AniDb.ClientPort);
            IsNetworkAvailable = _socketHandler.TryConnection();
        }

        _isLoggedOn = false;
        _pingTimer = new Timer { Interval = settings.AniDb.UDPPingFrequency * 1000, Enabled = true, AutoReset = true };
        _pingTimer.Elapsed += PingTimerElapsed;
        _logoutTimer = new Timer { Interval = LogoutPeriod, Enabled = true, AutoReset = false };
        _logoutTimer.Elapsed += LogoutTimerElapsed;

        IsAlive = true;
    }

    private void PingTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            if (!_isLoggedOn) return;
            lock (_socketHandlerLock)
                if (_socketHandler is not { IsConnected: true }) return;

            if (IsBanned || BackoffSecs.HasValue) return;

            var ping = _requestFactory.Create<RequestPing>();
            ping.Send();
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
        try
        {
            if (!_isLoggedOn) return;
            lock (_socketHandlerLock)
                if (_socketHandler is not { IsConnected: true }) return;
            if (IsBanned || BackoffSecs.HasValue) return;

            ForceLogout();
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
    /// <returns></returns>
    public string Send(string command, bool needsUnicode = true)
    {
        // Steps:
        // 1. Check Ban state and throw if Banned
        // 2. Check Login State and Login if needed
        // 3. Actually Call AniDB

        // Check Ban State
        // Ideally, this will never happen, as we stop the queue and attempt a graceful rollback of the command
        if (IsBanned)
        {
            throw new AniDBBannedException
            {
                BanType = UpdateType.UDPBan, BanExpires = BanTime?.AddHours(BanTimerResetLength)
            };
        }
        // TODO Low Priority: We need to handle Login Attempt Decay, so that we can try again if it's not just a bad user/pass
        // It wasn't handled before, and it's not caused serious problems

        // login doesn't use this method, so this check won't interfere with it
        // if we got here, and it's invalid session, then it already failed to re-log
        if (IsInvalidSession)
        {
            throw new NotLoggedInException();
        }

        lock (_socketHandlerLock)
        {
            // Check Login State
            if (!Login())
            {
                throw new LoginFailedException();
            }

            // Actually Call AniDB
            return SendInternal(command, needsUnicode);
        }
    }

    public string SendDirectly(string command, bool needsUnicode = true, bool isPing = false, bool isLogout = false)
    {
        lock(_socketHandlerLock)
            return SendInternal(command, needsUnicode, isPing);
    }

    private string SendInternal(string command, bool needsUnicode = true, bool isPing = false, bool isLogout = false)
    {
        ObjectDisposedException.ThrowIf(_socketHandler is not { IsConnected: true }, "The connection was closed by shoko");

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
            var sendByteAdd = encoding.GetBytes(command);
            var timeoutPolicy = Policy
                .Handle<SocketException>(e => e is { SocketErrorCode: SocketError.TimedOut })
                .Or<OperationCanceledException>()
                // retry once
                .Retry(1, (_, _) =>
                {
                    Logger.LogWarning("AniDB request timed out. Checking Network and trying again");
                    _connectivityService.CheckAvailability().Wait();
                });

            var result = timeoutPolicy.ExecuteAndCapture(() => _rateLimiter.EnsureRate(forceShortDelay: isPing, action: () =>
            {
                if (_connectivityService.NetworkAvailability < NetworkAvailability.PartialInternet)
                {
                    Logger.LogError("No internet, so not sending AniDB request");
                    throw new SocketException((int)SocketError.HostUnreachable);
                }

                var start = DateTime.Now;
                Logger.LogTrace("AniDB UDP Call: (Using {Unicode}) {Command}", needsUnicode ? "Unicode" : "ASCII", MaskLog(command));
                var byReceivedAdd = _socketHandler.Send(sendByteAdd);

                if (byReceivedAdd.All(a => a == 0))
                {
                    // we are probably banned or have lost connection. We can't tell the difference, so we're assuming ban
                    IsBanned = true;
                    throw new AniDBBannedException
                    {
                        BanType = UpdateType.UDPBan, BanExpires = BanTime?.AddHours(BanTimerResetLength)
                    };
                }

                // decode
                var decodedString = Utils.GetEncoding(byReceivedAdd).GetString(byReceivedAdd, 0, byReceivedAdd.Length);
                // remove BOM
                if (decodedString[0] == 0xFEFF) decodedString = decodedString[1..];

                var ts = DateTime.Now - start;
                Logger.LogTrace("AniDB Response: Received in {Time:ss'.'ffff}s\n{DecodedString}", ts, MaskLog(decodedString));
                return decodedString;
            }));

            if (result.FinalException != null)
            {
                Logger.LogError(result.FinalException, "Failed to send AniDB message");
                throw result.FinalException;
            }

            return result.Result;
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

    private void StopPinging()
    {
        _pingTimer?.Stop();
        _logoutTimer?.Stop();
    }

    public void ForceReconnection()
    {
        lock (_socketHandlerLock)
        {

            try
            {
                ForceLogout();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to logout");
            }

            try
            {
                CloseConnections();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to close socket");
            }

            try
            {
                InitInternal();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to reinitialize socket");
            }
        }
    }

    public void ForceLogout()
    {
        StopPinging();
        if (!_isLoggedOn) return;

        if (IsBanned)
        {
            _isLoggedOn = false;
            SessionID = null;
            return;
        }

        Logger.LogTrace("Logging Out");
        try
        {
            lock(_socketHandlerLock) _requestFactory.Create<RequestLogout>().Send();
        }
        catch
        {
            // ignore
        }

        _isLoggedOn = false;
        SessionID = null;
    }

    public void ClearSession()
    {
        StopPinging();
        IsInvalidSession = false;
        _isLoggedOn = false;
        SessionID = null;
    }

    public void CloseConnections()
    {
        IsNetworkAvailable = false;
        IsAlive = false;

        _pingTimer?.Stop();
        _pingTimer?.Dispose();
        _pingTimer = null;

        _logoutTimer?.Stop();
        _logoutTimer?.Dispose();
        _logoutTimer = null;

        lock(_socketHandlerLock)
        {
            if (_socketHandler == null) return;
            Logger.LogInformation("AniDB UDP Socket Disposing...");
            _socketHandler.Dispose();
            _socketHandler = null;
        }
    }

    public bool Login()
    {
        var settings = SettingsProvider.GetSettings();
        if (Login(settings.AniDb.Username, settings.AniDb.Password)) return true;

        try
        {
            if (IsBanned) return false;
            Logger.LogTrace("Failed to login to AniDB. Issuing a Logout command and retrying");
            lock (_socketHandlerLock)
            {
                ForceLogout();
                return Login(settings.AniDb.Username, settings.AniDb.Password);
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e, "{Message}", e);
        }

        return false;
    }

    private bool Login(string username, string password)
    {
        // check if we are already logged in
        if (_isLoggedOn) return true;

        if (!ValidAniDBCredentials(username, password))
        {
            LoginFailed?.Invoke(this, null!);
            return false;
        }

        Logger.LogTrace("Logging in");
        UDPResponse<ResponseLogin> response;
        try
        {
            response = LoginWithFallbacks(username, password);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Unable to login to AniDB");
            response = new UDPResponse<ResponseLogin>();
        }

        switch (response.Code)
        {
            case UDPReturnCode.LOGIN_FAILED:
                SessionID = null;
                IsInvalidSession = true;
                _isLoggedOn = false;
                Logger.LogError("AniDB Login Failed: invalid credentials");
                LoginFailed?.Invoke(this, null!);
                break;
            case UDPReturnCode.LOGIN_ACCEPTED:
                SessionID = response.Response.SessionID;
                _cdnDomain = response.Response.ImageServer;
                _isLoggedOn = true;
                IsInvalidSession = false;
                return true;
            default:
                SessionID = null;
                _isLoggedOn = false;
                IsInvalidSession = true;
                break;
        }

        return false;
    }

    private UDPResponse<ResponseLogin> LoginWithFallbacks(string username, string password)
    {
        try
        {
            var login = _requestFactory.Create<RequestLogin>(
                r =>
                {
                    r.Username = username;
                    r.Password = password;
                }
            );
            return login.Send();
        }
        catch (Exception e) when (e is UnexpectedUDPResponseException or NotLoggedInException)
        {
            Logger.LogTrace(
                "Received an UnexpectedUDPResponseException on Login. This usually happens because of an unexpected shutdown. Relogging using Unicode");
            var login = _requestFactory.Create<RequestLogin>(
                r =>
                {
                    r.Username = username;
                    r.Password = password;
                    r.UseUnicode = true;
                }
            );
            return login.Send();
        }
        catch (SocketException e)
            when (e.SocketErrorCode == SocketError.TimedOut)
        {
            Logger.LogTrace("Received a Timeout on Login. Restarting Socket and relogging");
            ForceReconnection();
            var login = _requestFactory.Create<RequestLogin>(
                r =>
                {
                    r.Username = username;
                    r.Password = password;
                }
            );
            return login.Send();
        }
        catch (SocketException e)
        {
            Logger.LogError(e, "Unable to login to AniDB");
            return new UDPResponse<ResponseLogin>();
        }
    }

    public bool TestLogin(string username, string password)
    {
        if (!ValidAniDBCredentials(username, password))
        {
            return false;
        }

        lock (_socketHandlerLock)
        {
            var result = Login(username, password);
            if (result)
            {
                ForceLogout();
            }

            return result;
        }
    }

    public bool SetCredentials(string username, string password)
    {
        if (!ValidAniDBCredentials(username, password))
        {
            return false;
        }

        var settings = SettingsProvider.GetSettings();
        settings.AniDb.Username = username;
        settings.AniDb.Password = password;
        SettingsProvider.SaveSettings();
        return true;
    }

    public bool ValidAniDBCredentials(string user, string pass)
    {
        if (string.IsNullOrEmpty(user)) return false;
        if (string.IsNullOrEmpty(pass)) return false;

        return true;
    }

    private static string MaskLog(string input)
    {
        return s_logMask.Replace(input, "****");
    }
}
