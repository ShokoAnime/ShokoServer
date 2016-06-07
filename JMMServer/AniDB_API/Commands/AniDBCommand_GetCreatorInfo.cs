using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_GetCreatorInfo : AniDBUDPCommand, IAniDBUDPCommand
    {
        public AniDBCommand_GetCreatorInfo()
        {
            commandType = enAniDBCommandType.GetCreatorInfo;
        }

        public int CreatorID { get; set; }

        public Raw_AniDB_Creator CreatorInfo { get; set; }

        public bool ForceRefresh { get; set; }

        public string GetKey()
        {
            return "AniDBCommand_GetCreatorInfo" + CreatorID;
        }

        public virtual enHelperActivityType GetStartEventType()
        {
            return enHelperActivityType.GettingCreatorInfo;
        }

        public virtual enHelperActivityType Process(ref Socket soUDP,
            ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
        {
            ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

            // handle 555 BANNED and 598 - UNKNOWN COMMAND
            if (ResponseCode == 598) return enHelperActivityType.UnknownCommand_598;
            if (ResponseCode == 555) return enHelperActivityType.Banned_555;

            if (errorOccurred) return enHelperActivityType.NoSuchCreator;

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetCreatorInfo.Process: Response: {0}", socketResponse);

            // Process Response
            var sMsgType = socketResponse.Substring(0, 3);


            switch (sMsgType)
            {
                case "245":
                    {
                        // 245 CREATOR
                        // the first 11 characters should be "245 CREATOR"
                        // the rest of the information should be the data list

                        CreatorInfo = new Raw_AniDB_Creator(socketResponse);
                        return enHelperActivityType.GotCreatorInfo;


                        // 245 CREATOR 200|?????|Suwabe Jun`ichi|1|17015.jpg||http://www.haikyo.or.jp/PROFILE/man/11470.html|Junichi_Suwabe|%E8%AB%8F%E8%A8%AA%E9%83%A8%E9%A0%86%E4%B8%80|1236300570
                    }
                case "345":
                    {
                        return enHelperActivityType.NoSuchCreator;
                    }
                case "501":
                    {
                        return enHelperActivityType.LoginRequired;
                    }
            }

            return enHelperActivityType.NoSuchCreator;
        }

        public void Init(int creaID, bool force)
        {
            CreatorID = creaID;
            ForceRefresh = force;
            commandText = "CREATOR creatorid=" + CreatorID;

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetCreatorInfo.Process: Request: {0}", commandText);

            commandID = CreatorID.ToString();
        }
    }
}