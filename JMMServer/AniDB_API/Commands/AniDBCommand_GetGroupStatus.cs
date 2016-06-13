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
            return "AniDBCommand_GetGroupStatus" + AnimeID.ToString();
        }

        private GroupStatusCollection grpStatus = null;

        public GroupStatusCollection GrpStatusCollection
        {
            get { return grpStatus; }
            set { grpStatus = value; }
        }

        public virtual enHelperActivityType GetStartEventType()
        {
            return enHelperActivityType.GettingGroupStatus;
        }

        public virtual enHelperActivityType Process(ref Socket soUDP,
            ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
        {
            ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

            // handle 555 BANNED and 598 - UNKNOWN COMMAND
            if (ResponseCode == 598) return enHelperActivityType.UnknownCommand_598;
            if (ResponseCode == 555) return enHelperActivityType.Banned_555;

            if (errorOccurred) return enHelperActivityType.NoGroupsFound;

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetGroupStatus.Process: Response: {0}", socketResponse);

            // Process Response
            string sMsgType = socketResponse.Substring(0, 3);


            switch (sMsgType)
            {
                case "225":
                {
                    // 225 GROUPSTATUS

                    grpStatus = new GroupStatusCollection(animeID, socketResponse);
                    return enHelperActivityType.GotGroupStatus;
                }
                case "330":
                {
                    return enHelperActivityType.NoSuchAnime;
                }
                case "325": // no such description
                {
                    return enHelperActivityType.NoGroupsFound;
                }
                case "501":
                {
                    return enHelperActivityType.LoginRequired;
                }
            }

            return enHelperActivityType.FileDoesNotExist;
        }

        public AniDBCommand_GetGroupStatus()
        {
            commandType = enAniDBCommandType.GetGroupStatus;
        }

        public void Init(int animeID)
        {
            this.animeID = animeID;
            commandText = "GROUPSTATUS aid=" + animeID.ToString();

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetGroupStatus.Process: Request: {0}", commandText);

            commandID = animeID.ToString();
        }
    }
}