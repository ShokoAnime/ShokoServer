using System;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.Interfaces
{
    public interface IUDPConnectionHandler : IConnectionHandler
    {
        bool IsNetworkAvailable { get; }
        event EventHandler LoginFailed;
        bool SetCredentials(string username, string password);
        bool ValidAniDBCredentials(string user, string pass);
        bool Login();
        void ForceLogout();
        void ForceReconnection();
        UDPBaseResponse<string> CallAniDBUDPDirectly(string command, bool needsUnicode, bool disableLogging, bool isPing, bool returnFullResponse);
        UDPBaseResponse<string> CallAniDBUDP(string command, bool needsUnicode, bool disableLogging, bool isPing);
    }
}
