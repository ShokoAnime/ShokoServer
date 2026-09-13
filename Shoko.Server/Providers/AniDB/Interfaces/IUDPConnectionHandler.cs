using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Server.Providers.AniDB.Interfaces;

/// <summary>
/// The narrow surface of the AniDB UDP connection handler that external
/// consumers (the acquisition filter, the event emitter, the services) read.
/// The full control surface (login, logout, reconnection, the raw send, the
/// session id, the backoff timer) lives on the concrete
/// <see cref="Shoko.Server.Providers.AniDB.UDP.AniDBUDPConnectionHandler"/> and
/// is used by the protocol classes and the debug/API entry points.
/// </summary>
public interface IUDPConnectionHandler : IConnectionHandler
{
    /// <summary>
    /// The current session is invalid and needs a re-login.
    /// </summary>
    bool IsInvalidSession { get; }

    /// <summary>
    /// Set when AniDB explicitly rejected the credentials (LOGIN_FAILED).
    /// Unlike <see cref="IsInvalidSession"/>, this is never cleared by a
    /// session reset — only a deliberate re-initialization clears it.
    /// </summary>
    bool IsLoginFailed { get; }

    /// <summary>
    /// Sends a command, ensuring a valid login first.
    /// </summary>
    /// <param name="command">The request to be made.</param>
    /// <param name="needsUnicode">Whether to ask for UTF16.</param>
    /// <param name="cancellationToken"></param>
    Task<string> SendAsync(string command, bool needsUnicode = true, CancellationToken cancellationToken = default);
}
