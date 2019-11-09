using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_NotifyList : AniDBUDPCommand, IAniDBUDPCommand
    {
        public string GetKey()
        {
            return "AniDBCommand_NotifyList";
        }

        private Raw_AniDB_NotifyList notifyListCollection = null;

        public Raw_AniDB_NotifyList NotifyListCollection
        {
            get { return notifyListCollection; }
            set { notifyListCollection = value; }
        }

        public virtual AniDBUDPResponseCode GetStartEventType()
        {
            return AniDBUDPResponseCode.GettingNotifyList;
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

            if (errorOccurred) return AniDBUDPResponseCode.LoginRequired;

            // Process Response
            string sMsgType = socketResponse.Substring(0, 3);


            switch (sMsgType)
            {
                case "291":
                {
                    notifyListCollection = new Raw_AniDB_NotifyList(socketResponse);
                    return AniDBUDPResponseCode.GotNotifyList;
                }
                case "501": return AniDBUDPResponseCode.LoginRequired;
            }

            return AniDBUDPResponseCode.GotNotifyList;
        }

        public AniDBCommand_NotifyList()
        {
            commandType = enAniDBCommandType.GetNotifyList;
        }

        public void Init()
        {
            commandText = "NOTIFYLIST ";

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetCalendar: Request: {0}", commandText);

            commandID = "NOTIFYLIST ";
        }
    }
}