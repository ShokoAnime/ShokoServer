using System.Net;
using System.Net.Sockets;
using System.Text;
using AniDBAPI;
using AniDBAPI.Commands;

namespace Shoko.Server.Commands
{
    public class AniDBCommand_GetGroup : AniDBUDPCommand, IAniDBUDPCommand
    {
        public int GroupID { get; set; }
        public Raw_AniDB_Group Group { get; set; }

        public string GetKey()
        {
            return "AniDBCommand_GetGroup_" + GroupID;
        }

        public virtual AniDBUDPResponseCode GetStartEventType()
        {
            return AniDBUDPResponseCode.GettingGroup;
        }

        public virtual AniDBUDPResponseCode Process(ref Socket soUDP,
            ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
        {
            ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

            // handle 555 BANNED and 598 - UNKNOWN COMMAND
            if (ResponseCode == 598) return AniDBUDPResponseCode.UnknownCommand_598;
            if (ResponseCode == 555) return AniDBUDPResponseCode.Banned_555;

            if (errorOccurred) return AniDBUDPResponseCode.NoSuchGroup;


            // Process Response
            string sMsgType = socketResponse.Substring(0, 3);


            switch (sMsgType)
            {
                case "250":
                {
                    // 250 GROUP
                    //3938|704|1900|53|1126|Ayako-Fansubs|Ayako|#Ayako|irc.rizon.net|http://ayakofansubs.info/|1669.png
                    Group = new Raw_AniDB_Group(socketResponse);
                    return AniDBUDPResponseCode.GotGroup;
                }
                case "350": return AniDBUDPResponseCode.NoSuchGroup;
                case "501": return AniDBUDPResponseCode.LoginRequired;
            }

            return AniDBUDPResponseCode.FileDoesNotExist;
        }

        public AniDBCommand_GetGroup()
        {
            commandType = enAniDBCommandType.GetGroup;
            Group = null;
        }

        public void Init(int groupID)
        {
            GroupID = groupID;
            commandText = "GROUP gid=" + GroupID;

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetGroupStatus.Process: Request: {0}", commandText);

            commandID = GroupID.ToString();
        }
    }
}