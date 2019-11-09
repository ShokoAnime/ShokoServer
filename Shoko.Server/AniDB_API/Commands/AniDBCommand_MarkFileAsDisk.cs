using System.Net;
using System.Net.Sockets;
using System.Text;
using Shoko.Models.Enums;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_MarkFileAsDisk : AniDBUDPCommand, IAniDBUDPCommand
    {
        public int MyListID = 0;

        public string GetKey()
        {
            return "AniDBCommand_MarkFileAsDisk_" + MyListID;
        }

        public virtual AniDBUDPResponseCode GetStartEventType()
        {
            return AniDBUDPResponseCode.MarkingFileRemote;
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

        public AniDBCommand_MarkFileAsDisk()
        {
            commandType = enAniDBCommandType.MarkFileDisk;
        }

        public void Init(int lid)
        {
            MyListID = lid;
            commandID = "MarkingFileDisk File: " + lid;

            commandText = "MYLISTADD lid=" + lid;
            commandText += "&state=" + (int) AniDBFile_State.Disk;
            commandText += "&edit=1";
        }
    }
}