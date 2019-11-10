using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_GetCreatorInfo : AniDBUDPCommand, IAniDBUDPCommand
    {
        private int creatorID = 0;

        public int CreatorID
        {
            get { return creatorID; }
            set { creatorID = value; }
        }

        public string GetKey()
        {
            return "AniDBCommand_GetCreatorInfo" + CreatorID.ToString();
        }

        private Raw_AniDB_Creator creatorInfo = null;

        public Raw_AniDB_Creator CreatorInfo
        {
            get { return creatorInfo; }
            set { creatorInfo = value; }
        }

        private bool forceRefresh = false;

        public bool ForceRefresh
        {
            get { return forceRefresh; }
            set { forceRefresh = value; }
        }

        public virtual AniDBUDPResponseCode GetStartEventType()
        {
            return AniDBUDPResponseCode.GettingCreatorInfo;
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

            if (errorOccurred) return AniDBUDPResponseCode.NoSuchCreator;

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetCreatorInfo.Process: Response: {0}", socketResponse);

            // Process Response
            string sMsgType = socketResponse.Substring(0, 3);


            switch (sMsgType)
            {
                case "245":
                {
                    // 245 CREATOR
                    // the first 11 characters should be "245 CREATOR"
                    // the rest of the information should be the data list

                    creatorInfo = new Raw_AniDB_Creator(socketResponse);
                    return AniDBUDPResponseCode.GotCreatorInfo;


                    // 245 CREATOR 200|?????|Suwabe Jun`ichi|1|17015.jpg||http://www.haikyo.or.jp/PROFILE/man/11470.html|Junichi_Suwabe|%E8%AB%8F%E8%A8%AA%E9%83%A8%E9%A0%86%E4%B8%80|1236300570
                }
                case "345": return AniDBUDPResponseCode.NoSuchCreator;
                case "501": return AniDBUDPResponseCode.LoginRequired;
            }

            return AniDBUDPResponseCode.NoSuchCreator;
        }


        public AniDBCommand_GetCreatorInfo()
        {
            commandType = enAniDBCommandType.GetCreatorInfo;
        }

        public void Init(int creaID, bool force)
        {
            this.creatorID = creaID;
            this.forceRefresh = force;
            commandText = "CREATOR creatorid=" + creatorID.ToString();

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetCreatorInfo.Process: Request: {0}", commandText);

            commandID = creatorID.ToString();
        }
    }
}