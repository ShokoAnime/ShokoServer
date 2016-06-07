using System.Net;
using System.Net.Sockets;
using System.Text;
using JMMServer.AniDB_API.Raws;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_GetMyListStats : AniDBUDPCommand, IAniDBUDPCommand
    {
        public AniDBCommand_GetMyListStats()
        {
            commandType = enAniDBCommandType.GetMyListStats;
        }

        public Raw_AniDB_MyListStats MyListStats { get; set; }

        public string GetKey()
        {
            return "AniDBCommand_GetMyListStats";
        }

        public virtual enHelperActivityType GetStartEventType()
        {
            return enHelperActivityType.GettingMyListStats;
        }

        public virtual enHelperActivityType Process(ref Socket soUDP,
            ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
        {
            ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

            // handle 555 BANNED and 598 - UNKNOWN COMMAND
            if (ResponseCode == 598) return enHelperActivityType.UnknownCommand_598;
            if (ResponseCode == 555) return enHelperActivityType.Banned_555;

            if (errorOccurred) return enHelperActivityType.NoSuchMyListFile;


            // Process Response
            var sMsgType = socketResponse.Substring(0, 3);


            switch (sMsgType)
            {
                case "222":
                    {
                        MyListStats = new Raw_AniDB_MyListStats(socketResponse);
                        return enHelperActivityType.GotMyListStats;
                    }
                case "501":
                    {
                        return enHelperActivityType.LoginRequired;
                    }
            }

            return enHelperActivityType.NoSuchMyListFile;
        }

        public void Init()
        {
            commandText = "MYLISTSTATS ";
            commandID = "AniDBCommand_GetMyListStats";
        }
    }
}