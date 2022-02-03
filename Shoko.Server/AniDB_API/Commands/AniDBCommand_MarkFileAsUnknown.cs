using System.Net;
using System.Net.Sockets;
using System.Text;
using Shoko.Models.Enums;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_MarkFileAsUnknown : AniDBUDPCommand, IAniDBUDPCommand
    {
        public int MyListID;

        public string GetKey()
        {
            return "AniDBCommand_MarkFileAsUnknown_" + MyListID;
        }

        public virtual AniDBUDPResponseCode GetStartEventType()
        {
            return AniDBUDPResponseCode.MarkingFileUnknown;
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

            if (errorOccurred) return AniDBUDPResponseCode.NoSuchFile;

            string sMsgType = socketResponse.Substring(0, 3);
            switch (sMsgType)
            {
                case "210": return AniDBUDPResponseCode.FileMarkedAsDeleted;
                case "310": return AniDBUDPResponseCode.FileMarkedAsDeleted;
                case "311": return AniDBUDPResponseCode.FileMarkedAsDeleted;
                case "320": return AniDBUDPResponseCode.NoSuchFile;
                case "411": return AniDBUDPResponseCode.NoSuchFile;

                case "502": return AniDBUDPResponseCode.LoginFailed;
                case "501": return AniDBUDPResponseCode.LoginRequired;
            }

            return AniDBUDPResponseCode.FileDoesNotExist;
        }

        public AniDBCommand_MarkFileAsUnknown()
        {
            commandType = enAniDBCommandType.MarkFileUnknown;
        }

        public void Init(int lid)
        {
            MyListID = lid;
            commandID = "MarkingFileUnknown File: " + lid;

            commandText = "MYLISTADD lid=" + lid;
            commandText += "&state=" + (int) AniDBFile_State.Unknown;
            commandText += "&edit=1";
        }
    }
}