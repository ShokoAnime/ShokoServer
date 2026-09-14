using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Connection;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Providers.AniDB.UDP;

/// <summary>
/// The AniDB UDP session: the logged-in state, the session ID, and the
/// login/logout protocol. The owning <see cref="AniDBUDPConnectionHandler"/>
/// composes this with the socket transport and owns the socket lock and the
/// ping timers, so every method here is expected to be invoked under the
/// handler's socket lock.
/// </summary>
public sealed class AniDbUdpSession
{
    private readonly IUdpSessionTransport _transport;
    private readonly IRequestFactory _requestFactory;
    private readonly ISettingsProvider _settingsProvider;
    private readonly ILogger _logger;

    internal AniDbUdpSession(IUdpSessionTransport transport, IRequestFactory requestFactory, ISettingsProvider settingsProvider, ILogger logger)
    {
        _transport = transport;
        _requestFactory = requestFactory;
        _settingsProvider = settingsProvider;
        _logger = logger;
    }

    public string? SessionID { get; private set; }

    public bool IsLoggedOn { get; private set; }

    /// <summary>
    /// The image CDN domain reported by the server on login, used to build
    /// <see cref="AniDBUDPConnectionHandler.ImageServerUrl"/>.
    /// </summary>
    public string CdnDomain { get; private set; } = Constants.AnidbCdnUrl;

    public event EventHandler? LoginFailed;

    private bool _isInvalidSession;

    public bool IsInvalidSession
    {
        get => _isInvalidSession;
        internal set
        {
            _isInvalidSession = value;
            _transport.UpdateState(new AniDBStateUpdate
            {
                UpdateType = UpdateType.InvalidSession,
                UpdateTime = DateTime.Now,
                Value = value
            });
        }
    }

    private bool _isLoginFailed;

    /// <summary>
    /// Set when AniDB explicitly rejects credentials (LOGIN_FAILED response).
    /// Unlike <see cref="IsInvalidSession"/>, this is never cleared by session-reset or ban-expiry
    /// paths — only a deliberate re-initialization (which implies credentials may have changed)
    /// clears it. The acquisition filter uses this to hold AniDB jobs permanently until the user
    /// fixes their credentials.
    /// </summary>
    public bool IsLoginFailed
    {
        get => _isLoginFailed;
        internal set
        {
            _isLoginFailed = value;
            _transport.UpdateState(new AniDBStateUpdate
            {
                UpdateType = UpdateType.LoginFailed,
                UpdateTime = DateTime.Now,
                Value = value
            });
        }
    }

    public static bool ValidAniDBCredentials([NotNullWhen(true)] string? user, [NotNullWhen(true)] string? pass)
    {
        if (string.IsNullOrEmpty(user)) return false;
        if (string.IsNullOrEmpty(pass)) return false;

        return true;
    }

    public bool SetCredentials(string username, string password)
    {
        if (!ValidAniDBCredentials(username, password))
        {
            return false;
        }

        var settings = _settingsProvider.GetSettings();
        settings.AniDb.Username = username;
        settings.AniDb.Password = password;
        _settingsProvider.SaveSettings();
        return true;
    }

    /// <summary>
    /// Logs in with the supplied credentials, issuing a logout and retrying
    /// once when the first attempt fails for a non-permanent reason.
    /// </summary>
    public async Task<bool> LoginAsync(string? username, string? password, CancellationToken cancellationToken)
    {
        if (await LoginCoreAsync(username, password, cancellationToken))
        {
            return true;
        }

        try
        {
            if (_transport.IsBanned || IsLoginFailed) return false;
            _logger.LogTrace("Failed to login to AniDB. Issuing a Logout command and retrying");
            await ForceLogoutCoreAsync(cancellationToken);
            return await LoginCoreAsync(username, password, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{Message}", e);
            return false;
        }
    }

    internal async Task<bool> LoginCoreAsync(string? username, string? password, CancellationToken cancellationToken)
    {
        // check if we are already logged in
        if (IsLoggedOn) return true;

        if (!ValidAniDBCredentials(username, password))
        {
            LoginFailed?.Invoke(this, null!);
            return false;
        }

        _logger.LogTrace("Logging in");
        UDPResponse<ResponseLogin> response;
        try
        {
            response = await LoginWithFallbacksAsync(username, password, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to login to AniDB");
            response = new UDPResponse<ResponseLogin>();
        }

        switch (response.Code)
        {
            case UDPReturnCode.LOGIN_FAILED:
                SessionID = null;
                IsInvalidSession = true;
                IsLoginFailed = true;
                IsLoggedOn = false;
                _logger.LogError("AniDB Login Failed: invalid credentials");
                LoginFailed?.Invoke(this, null!);
                break;
            case UDPReturnCode.LOGIN_ACCEPTED:
                SessionID = response.Response.SessionID;
                CdnDomain = $"https://{response.Response.ImageServer}";
                IsLoggedOn = true;
                IsInvalidSession = false;
                return true;
            default:
                SessionID = null;
                IsLoggedOn = false;
                IsInvalidSession = true;
                break;
        }

        return false;
    }

    private async Task<UDPResponse<ResponseLogin>> LoginWithFallbacksAsync(string username, string password, CancellationToken cancellationToken)
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
            return await login.SendAsync(cancellationToken);
        }
        catch (Exception e) when (e is UnexpectedUDPResponseException or NotLoggedInException)
        {
            _logger.LogTrace(
                "Received an UnexpectedUDPResponseException on Login. This usually happens because of an unexpected shutdown. Relogging using Unicode");
            var login = _requestFactory.Create<RequestLogin>(
                r =>
                {
                    r.Username = username;
                    r.Password = password;
                    r.UseUnicode = true;
                }
            );
            return await login.SendAsync(cancellationToken);
        }
        catch (Exception e) when (IsTimeout(e, cancellationToken))
        {
            _logger.LogTrace("Received a Timeout on Login. Restarting Socket and relogging");
            await _transport.ForceReconnectionAsync(cancellationToken);
            var login = _requestFactory.Create<RequestLogin>(
                r =>
                {
                    r.Username = username;
                    r.Password = password;
                }
            );
            return await login.SendAsync(cancellationToken);
        }
        catch (SocketException e)
        {
            _logger.LogError(e, "Unable to login to AniDB");
            return new UDPResponse<ResponseLogin>();
        }
    }

    internal async Task ForceLogoutCoreAsync(CancellationToken cancellationToken)
    {
        _transport.StopPinging();
        if (!IsLoggedOn) return;

        if (_transport.IsBanned)
        {
            IsLoggedOn = false;
            SessionID = null;
            return;
        }

        _logger.LogTrace("Logging Out");
        try
        {
            var logout = _requestFactory.Create<RequestLogout>();
            await logout.SendAsync(cancellationToken);
        }
        catch
        {
            // ignore
        }

        IsLoggedOn = false;
        SessionID = null;
    }

    /// <summary>
    /// Clears the session state. The caller (the handler) is responsible for
    /// stopping the ping timers, which the session cannot own.
    /// </summary>
    internal void ClearSessionState()
    {
        IsInvalidSession = false;
        IsLoggedOn = false;
        SessionID = null;
    }

    /// <summary>
    /// Resets the session flags during (re)initialization of the socket.
    /// </summary>
    internal void ResetState()
    {
        IsLoggedOn = false;
        IsInvalidSession = false;
        IsLoginFailed = false;
    }

    /// <summary>
    /// Applies the session-side effects of a ban change: a fresh ban ends the
    /// login and clears the session, and any ban change invalidates the session.
    /// </summary>
    internal void HandleBan(bool banned)
    {
        if (banned)
        {
            IsLoggedOn = false;
            SessionID = null;
        }

        IsInvalidSession = false;
    }

    private static bool IsTimeout(Exception exception, CancellationToken cancellationToken)
        => exception is SocketException { SocketErrorCode: SocketError.TimedOut }
           || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);
}
