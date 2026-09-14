using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Server.Providers.AniDB.UDP;

/// <summary>
/// The slice of the AniDB UDP channel that a <see cref="Generic.UDPRequest{T}"/>
/// uses to send its command and to apply the protocol-level side effects of the
/// response (ban, backoff, session reset). It is kept separate from the narrow
/// public <see cref="Interfaces.IUDPConnectionHandler"/> — which only exposes what
/// the acquisition filter and services read — so the request classes stay
/// unit-testable with a mock.
/// </summary>
public interface IAniDbUdpRequestChannel
{
    string? SessionID { get; }

    AniDbBanState BanState { get; }

    bool IsInvalidSession { get; set; }

    void StartBackoffTimer(int secsToPause, string pauseReason);

    void ClearSession();

    Task<bool> LoginAsync(CancellationToken cancellationToken);

    Task<string> SendAsync(string command, bool needsUnicode = true, CancellationToken cancellationToken = default);

    Task<string> SendDirectlyAsync(string command, bool needsUnicode = true, bool isPing = false, bool isLogout = false, CancellationToken cancellationToken = default);
}
