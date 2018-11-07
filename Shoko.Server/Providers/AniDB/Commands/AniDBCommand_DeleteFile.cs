using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Shoko.Server.Providers.AniDB.Commands
{
    public class AniDBCommand_DeleteFile : AniDBUDPCommand, IAniDBUDPCommand
    {
        public string Hash = string.Empty;
        public long FileSize = 0;
        public int FileID = 0;

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
            switch (ResponseCode)
            {
                case 598: return enHelperActivityType.UnknownCommand_598;
                case 555: return enHelperActivityType.Banned_555;
            }


            if (errorOccurred) return enHelperActivityType.NoSuchFile;

            string sMsgType = socketResponse.Substring(0, 3);
            switch (sMsgType)
            {
                case "211": return enHelperActivityType.FileDeleted;
                case "411": return enHelperActivityType.NoSuchFile;
                case "502": return enHelperActivityType.LoginFailed;
                case "501": return enHelperActivityType.LoginRequired;
            }

            return enHelperActivityType.NoSuchFile;
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

            commandText = "MYLISTDEL size=" + FileSize.ToString();
            commandText += "&ed2k=" + Hash;
        }

        public void Init(int fileID)
        {
            Hash = string.Empty;
            FileSize = 0;
            FileID = fileID;

            commandID = "Deleting File: " + fileID.ToString();

            commandText = "MYLISTDEL lid=" + fileID.ToString();
        }
    }
}