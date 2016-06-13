using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_NotifyGet : AniDBUDPCommand, IAniDBUDPCommand
    {
        public string messageType;
        public long messageID;

        public Raw_AniDB_NotifyMessage NotifyMessage = null;
        public Raw_AniDB_NotifyAlert NotifyAlert = null;

        public string GetKey()
        {
            return "AniDBCommand_NotifyGet";
        }

        public virtual enHelperActivityType GetStartEventType()
        {
            return enHelperActivityType.GettingNotifyGet;
        }

        public virtual enHelperActivityType Process(ref Socket soUDP,
            ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
        {
            ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

            // handle 555 BANNED and 598 - UNKNOWN COMMAND
            if (ResponseCode == 598) return enHelperActivityType.UnknownCommand_598;
            if (ResponseCode == 555) return enHelperActivityType.Banned_555;

            if (errorOccurred) return enHelperActivityType.NoSuchNotify;

            // Process Response
            string sMsgType = socketResponse.Substring(0, 3);


            switch (sMsgType)
            {
                case "292":
                {
                    NotifyMessage = new Raw_AniDB_NotifyMessage(socketResponse);
                    return enHelperActivityType.GotNotifyGet;
                }

                case "293":
                {
                    NotifyAlert = new Raw_AniDB_NotifyAlert(socketResponse);
                    return enHelperActivityType.GotNotifyGet;
                }
                case "392":
                case "393":
                {
                    return enHelperActivityType.NoSuchNotify;
                }
                case "501":
                {
                    return enHelperActivityType.LoginRequired;
                }
            }

            return enHelperActivityType.GotNotifyList;
        }

        public AniDBCommand_NotifyGet()
        {
            commandType = enAniDBCommandType.GetNotifyGet;
        }

        public void Init(string messageType, long messageID)
        {
            this.messageType = messageType;
            this.messageID = messageID;

            commandText = string.Format("NOTIFYGET type={0}&id={1}", messageType.Trim().ToUpper(), messageID);

            commandID = "NOTIFYGET ";
        }
    }
}