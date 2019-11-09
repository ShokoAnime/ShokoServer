using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_Ping : AniDBUDPCommand, IAniDBUDPCommand
    {
        public string SessionID = string.Empty;

        public virtual AniDBUDPResponseCode GetStartEventType()
        {
            return AniDBUDPResponseCode.Ping;
        }

        public string GetKey()
        {
            return "Ping";
        }

        public virtual AniDBUDPResponseCode Process(ref Socket soUDP,
            ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
        {
            ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

            // handle 555 BANNED and 598 - UNKNOWN COMMAND
            switch (ResponseCode)
            {
                case 598: return AniDBUDPResponseCode.UnknownCommand_598;
                case 555: return AniDBUDPResponseCode.Banned_555;
            }

            if (errorOccurred) return AniDBUDPResponseCode.PingFailed;

            // Process Response
            string sMsgType = socketResponse.Substring(0, 3);

            // 300 PONG
            return sMsgType.Equals("300") ? AniDBUDPResponseCode.PingFailed : AniDBUDPResponseCode.PingPong;
        }

        public AniDBCommand_Ping()
        {
            commandType = enAniDBCommandType.Ping;
            commandID = string.Empty;
        }

        public void Init()
        {
            commandText = "PING";
            commandID = "PING";
        }
    }
}