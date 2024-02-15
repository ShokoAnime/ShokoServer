using System;
using System.Threading.Tasks;

namespace Shoko.Server.Providers.AniDB.Interfaces;

public interface IUDPConnectionHandler : IConnectionHandler
{
    string SessionID { get; }
    string ImageServerUrl { get; }
    bool IsInvalidSession { set; get; }
    bool IsNetworkAvailable { get; }
    event EventHandler LoginFailed;
    bool SetCredentials(string username, string password);
    bool ValidAniDBCredentials(string user, string pass);
    Task<bool> Login();
    Task ForceLogout();
    Task CloseConnections();
    Task ForceReconnection();
    void ExtendBanTimer(int time, string message);
    Task<bool> Init(string username, string password, string serverName, ushort serverPort, ushort clientPort);
    Task<bool> TestLogin(string username, string password);

    Task<string> CallAniDBUDPDirectly(string command, bool needsUnicode = true, bool isPing = false);

    Task<string> CallAniDBUDP(string command, bool needsUnicode = true, bool isPing = false);
}
