using System;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.Interfaces
{
    public interface IUDPConnectionHandler : IConnectionHandler
    {
        string SessionID { get; }
        IServiceProvider ServiceProvider { get; }
        bool IsNetworkAvailable { get; }
        event EventHandler LoginFailed;
        bool SetCredentials(string username, string password);
        bool ValidAniDBCredentials(string user, string pass);
        bool Login();
        void ForceLogout();
        void ForceReconnection();
        UDPResponse<string> CallAniDBUDPDirectly(string command, bool needsUnicode=true, bool disableLogging=false, bool isPing=false, bool returnFullResponse=false);
        UDPResponse<string> CallAniDBUDP(string command, bool needsUnicode = true, bool disableLogging = false, bool isPing = false);
    }
}
