using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_GetGroupStatus : AniDBUDPCommand, IAniDBUDPCommand
    {
        private int animeID;

        public int AnimeID
        {
            get { return animeID; }
            set { animeID = value; }
        }

        public string GetKey()
        {
            return "AniDBCommand_GetGroupStatus" + AnimeID;
        }

        private GroupStatusCollection grpStatus;

        public GroupStatusCollection GrpStatusCollection
        {
            get { return grpStatus; }
            set { grpStatus = value; }
        }

        public virtual AniDBUDPResponseCode GetStartEventType()
        {
            return AniDBUDPResponseCode.GettingGroupStatus;
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

            if (errorOccurred) return AniDBUDPResponseCode.NoGroupsFound;

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetGroupStatus.Process: Response: {0}", socketResponse);

            // Process Response
            string sMsgType = socketResponse.Substring(0, 3);


            switch (sMsgType)
            {
                case "225":
                {
                    // 225 GROUPSTATUS

                    grpStatus = new GroupStatusCollection(animeID, socketResponse);
                    return AniDBUDPResponseCode.GotGroupStatus;
                }
                case "330": return AniDBUDPResponseCode.NoSuchAnime;
                case "325": // no such description
                    return AniDBUDPResponseCode.NoGroupsFound;
                case "501": return AniDBUDPResponseCode.LoginRequired;
            }

            return AniDBUDPResponseCode.FileDoesNotExist;
        }

        public AniDBCommand_GetGroupStatus()
        {
            commandType = enAniDBCommandType.GetGroupStatus;
        }

        public void Init(int animeID)
        {
            this.animeID = animeID;
            commandText = "GROUPSTATUS aid=" + animeID;

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetGroupStatus.Process: Request: {0}", commandText);

            commandID = animeID.ToString();
        }
    }
}