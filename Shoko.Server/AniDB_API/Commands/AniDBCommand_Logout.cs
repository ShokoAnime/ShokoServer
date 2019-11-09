using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_Logout : AniDBUDPCommand, IAniDBUDPCommand
    {
        public virtual AniDBUDPResponseCode GetStartEventType()
        {
            return AniDBUDPResponseCode.LoggingOut;
        }

        public string GetKey()
        {
            return "Logout";
        }

        public virtual AniDBUDPResponseCode Process(ref Socket soUDP,
            ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
        {
            ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

            return AniDBUDPResponseCode.LoggedOut;
        }

        public AniDBCommand_Logout()
        {
            commandType = enAniDBCommandType.Logout;
            commandID = string.Empty;
        }

        public void Init()
        {
            commandText = "LOGOUT ";
        }
    }
}