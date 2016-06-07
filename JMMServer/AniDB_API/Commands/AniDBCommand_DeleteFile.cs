using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_DeleteFile : AniDBUDPCommand, IAniDBUDPCommand
    {
        public int FileID;
        public long FileSize;
        public string Hash = "";

        public AniDBCommand_DeleteFile()
        {
            commandType = enAniDBCommandType.DeleteFile;
        }

        public string GetKey()
        {
            return "AniDBCommand_DeleteFile" + Hash;
        }

        public virtual enHelperActivityType GetStartEventType()
        {
            return enHelperActivityType.DeletingFile;
        }

        public virtual enHelperActivityType Process(ref Socket soUDP,
            ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
        {
            ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

            // handle 555 BANNED and 598 - UNKNOWN COMMAND
            if (ResponseCode == 598) return enHelperActivityType.UnknownCommand_598;
            if (ResponseCode == 555) return enHelperActivityType.Banned_555;

            if (errorOccurred) return enHelperActivityType.NoSuchFile;

            var sMsgType = socketResponse.Substring(0, 3);
            switch (sMsgType)
            {
                case "211":
                    return enHelperActivityType.FileDeleted;
                case "411":
                    return enHelperActivityType.NoSuchFile;
                case "502":
                    return enHelperActivityType.LoginFailed;
                case "501":
                    return enHelperActivityType.LoginRequired;
            }

            return enHelperActivityType.NoSuchFile;
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
            Hash = "";
            FileSize = 0;
            FileID = fileID;

            commandID = "Deleting File: " + fileID;

            commandText = "MYLISTDEL fid=" + fileID;
        }
    }
}