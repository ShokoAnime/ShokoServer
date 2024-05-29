using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Providers.AniDB.Interfaces;

namespace Shoko.Server.Providers.AniDB;

public class AniDBService : IAniDBService
{
    private readonly IUDPConnectionHandler _udpConnectionHandler;
    private readonly IHttpConnectionHandler _httpConnectionHandler;

    public AniDBService(IUDPConnectionHandler udpConnectionHandler, IHttpConnectionHandler httpConnectionHandler)
    {
        _udpConnectionHandler = udpConnectionHandler;
        _httpConnectionHandler = httpConnectionHandler;
    }

    public bool IsAniDBUdpReachable => _udpConnectionHandler.IsAlive && _udpConnectionHandler.IsNetworkAvailable;
    public bool IsAniDBUdpBanned => _udpConnectionHandler.IsBanned;
    public bool IsAniDBHttpBanned => _httpConnectionHandler.IsBanned;
}
