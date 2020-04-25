using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_DeleteFile : AniDBUDPCommand, IAniDBUDPCommand
    {
        public string Hash = string.Empty;
        public long FileSize;
        public int FileID;

        public string GetKey()
        {
            return "AniDBCommand_DeleteFile" + Hash;
        }

        public virtual AniDBUDPResponseCode GetStartEventType()
        {
            return AniDBUDPResponseCode.DeletingFile;
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
                case "211": return AniDBUDPResponseCode.FileDeleted;
                case "411": return AniDBUDPResponseCode.NoSuchFile;
                case "502": return AniDBUDPResponseCode.LoginFailed;
                case "501": return AniDBUDPResponseCode.LoginRequired;
            }

            return AniDBUDPResponseCode.NoSuchFile;
        }

        public AniDBCommand_DeleteFile()
        {
            commandType = enAniDBCommandType.DeleteFile;
        }

        public void Init(string hash, long fileSize)
        {
            Hash = hash;
            FileSize = fileSize;
            FileID = 0;

            commandID = "Deleting File: " + Hash;

            commandText = "MYLISTDEL size=" + FileSize;
            commandText += "&ed2k=" + Hash;
        }

        public void Init(int fileID)
        {
            Hash = string.Empty;
            FileSize = 0;
            FileID = fileID;

            commandID = "Deleting File: " + fileID;

            commandText = "MYLISTDEL lid=" + fileID;
        }
    }
}