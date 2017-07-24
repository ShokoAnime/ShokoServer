﻿using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_Ping : AniDBUDPCommand, IAniDBUDPCommand
    {
        public string SessionID = string.Empty;

        public virtual enHelperActivityType GetStartEventType()
        {
            return enHelperActivityType.Ping;
        }

        public string GetKey()
        {
            return "Ping";
        }

        public virtual enHelperActivityType Process(ref Socket soUDP,
            ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
        {
            ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

            // handle 555 BANNED and 598 - UNKNOWN COMMAND
            switch (ResponseCode)
            {
                case 598: return enHelperActivityType.UnknownCommand_598;
                case 555: return enHelperActivityType.Banned_555;
            }

            if (errorOccurred) return enHelperActivityType.PingFailed;

            // Process Response
            string sMsgType = socketResponse.Substring(0, 3);

            // 300 PONG
            return sMsgType.Equals("300") ? enHelperActivityType.PingFailed : enHelperActivityType.PingPong;
        }

        public AniDBCommand_Ping()
        {
            commandType = enAniDBCommandType.Ping;
            commandID = "";
        }

        public void Init()
        {
            commandText = "PING";
            commandID = "PING";
        }
    }
}