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

        public virtual enHelperActivityType GetStartEventType()
        {
            return enHelperActivityType.GettingNotifyList;
        }

        public virtual enHelperActivityType Process(ref Socket soUDP,
            ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
        {
            ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

            // handle 555 BANNED and 598 - UNKNOWN COMMAND
            if (ResponseCode == 598) return enHelperActivityType.UnknownCommand_598;
            if (ResponseCode == 555) return enHelperActivityType.Banned_555;

            if (errorOccurred) return enHelperActivityType.LoginRequired;

            // Process Response
            string sMsgType = socketResponse.Substring(0, 3);


            switch (sMsgType)
            {
                case "291":
                {
                    notifyListCollection = new Raw_AniDB_NotifyList(socketResponse);
                    return enHelperActivityType.GotNotifyList;
                }
                case "501":
                {
                    return enHelperActivityType.LoginRequired;
                }
            }

            return enHelperActivityType.GotNotifyList;
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