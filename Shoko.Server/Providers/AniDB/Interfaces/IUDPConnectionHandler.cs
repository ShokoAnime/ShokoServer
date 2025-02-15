using System;

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
    bool Login();
    void ForceLogout();
    void ClearSession();
    void CloseConnections();
    void ForceReconnection();
    void StartBackoffTimer(int time, string message);
    bool Init();
    bool Init(string username, string password, string serverName, ushort serverPort, ushort clientPort);
    bool TestLogin(string username, string password);

    string SendDirectly(string command, bool needsUnicode = true, bool isPing = false, bool isLogout = false);

    string Send(string command, bool needsUnicode = true);
}
