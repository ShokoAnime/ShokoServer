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
    void CloseConnections();
    void ForceReconnection();
    void ExtendBanTimer(int time, string message);
    bool Init(string username, string password, string serverName, ushort serverPort, ushort clientPort);
    bool TestLogin(string username, string password);

    string CallAniDBUDPDirectly(string command, bool needsUnicode = true, bool disableLogging = false,
        bool isPing = false);

    string CallAniDBUDP(string command, bool needsUnicode = true, bool disableLogging = false, bool isPing = false);
}
