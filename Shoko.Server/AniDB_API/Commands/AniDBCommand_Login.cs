using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_Login : AniDBUDPCommand, IAniDBUDPCommand
    {
        public string SessionID = string.Empty;

        public virtual AniDBUDPResponseCode GetStartEventType()
        {
            return AniDBUDPResponseCode.LoggingIn;
        }

        public string GetKey()
        {
            return "Login";
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

            if (errorOccurred) return AniDBUDPResponseCode.OtherError;

            // Process Response
            string sMsgType = socketResponse.Substring(0, 3);
            //BaseConfig.MyAnimeLog.Write("AniDBCommand_Login.Process: Response: {0}", socketResponse);

            // 200 {str session_key} LOGIN ACCEPTED
            // 203 LOGGED OUT
            // 500 LOGIN FAILED
            if (sMsgType.Equals("500"))
                return AniDBUDPResponseCode.LoginFailed;

            if (!sMsgType.Equals("200") && !sMsgType.Equals("201"))
                return AniDBUDPResponseCode.OtherError;

            // Get the session ID
            string sMessage = socketResponse.Substring(4);
            SessionID = sMessage.Trim();
            int i = sMessage.IndexOf("LOGIN");
            SessionID = sMessage.Substring(0, i - 1).Trim();

            return AniDBUDPResponseCode.LoggedIn;
        }

        public AniDBCommand_Login()
        {
            commandType = enAniDBCommandType.Login;
            commandID = string.Empty;
        }

        public void Init(string userName, string password)
        {
            commandText = "AUTH user=" + userName.Trim();
            commandText += "&pass=" + password.Trim();
            commandText += "&protover=3";
            commandText += "&client=ommserver";
            //commandText += "&client=vmcanidb";
            commandText += "&clientver=2&comp=1";
            //BaseConfig.MyAnimeLog.Write("commandText: {0}", commandText);
        }
    }
}