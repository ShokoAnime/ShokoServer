using System;
using System.Threading;
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
    void ForceLogout();
    void ClearSession();
    Task CloseConnections();
    Task ForceReconnection();
    void StartBackoffTimer(int time, string message);
    Task<bool> Init();
    Task<bool> Init(string username, string password, string serverName, ushort serverPort, ushort clientPort);
    Task<bool> TestLogin(string username, string password);

    Task<string> SendDirectly(string command, bool needsUnicode = true, bool isPing = false, bool isLogout = false, CancellationToken token = new());

    Task<string> Send(string command, bool needsUnicode = true, CancellationToken token = new());
}
