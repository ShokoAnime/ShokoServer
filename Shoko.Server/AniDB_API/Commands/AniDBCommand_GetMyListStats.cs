using System.Net;
using System.Net.Sockets;
using System.Text;
using Shoko.Server.AniDB_API.Raws;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_GetMyListStats : AniDBUDPCommand, IAniDBUDPCommand
    {
        public string GetKey()
        {
            return "AniDBCommand_GetMyListStats";
        }

        private Raw_AniDB_MyListStats myListStats;

        public Raw_AniDB_MyListStats MyListStats
        {
            get { return myListStats; }
            set { myListStats = value; }
        }

        public virtual AniDBUDPResponseCode GetStartEventType()
        {
            return AniDBUDPResponseCode.GettingMyListStats;
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

            if (errorOccurred) return AniDBUDPResponseCode.NoSuchMyListFile;


            // Process Response
            string sMsgType = socketResponse.Substring(0, 3);


            switch (sMsgType)
            {
                case "222":
                {
                    myListStats = new Raw_AniDB_MyListStats(socketResponse);
                    return AniDBUDPResponseCode.GotMyListStats;
                }
                case "501": return AniDBUDPResponseCode.LoginRequired;
            }

            return AniDBUDPResponseCode.NoSuchMyListFile;
        }

        public AniDBCommand_GetMyListStats()
        {
            commandType = enAniDBCommandType.GetMyListStats;
        }

        public void Init()
        {
            commandText = "MYLISTSTATS ";
            commandID = "AniDBCommand_GetMyListStats";
        }
    }
}