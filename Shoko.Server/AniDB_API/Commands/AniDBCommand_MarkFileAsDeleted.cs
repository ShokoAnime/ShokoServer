using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_MarkFileAsDeleted : AniDBUDPCommand, IAniDBUDPCommand
    {
        public bool ReturnIsWatched = false;
        public string Hash = "";

        public string GetKey()
        {
            return "AniDBCommand_MarkFileAsDeleted" + Hash;
        }

        public virtual enHelperActivityType GetStartEventType()
        {
            return enHelperActivityType.MarkingFileDeleted;
        }

        public virtual enHelperActivityType Process(ref Socket soUDP,
            ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
        {
            ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

            // handle 555 BANNED and 598 - UNKNOWN COMMAND
            if (ResponseCode == 598) return enHelperActivityType.UnknownCommand_598;
            if (ResponseCode == 555) return enHelperActivityType.Banned_555;

            if (errorOccurred) return enHelperActivityType.NoSuchFile;

            string sMsgType = socketResponse.Substring(0, 3);
            switch (sMsgType)
            {
                case "210":
                    return enHelperActivityType.FileMarkedAsDeleted;
                case "310":
                    return enHelperActivityType.FileMarkedAsDeleted;
                case "311":
                    return enHelperActivityType.FileMarkedAsDeleted;
                case "320":
                    return enHelperActivityType.NoSuchFile;
                case "411":
                    return enHelperActivityType.NoSuchFile;

                case "502":
                    return enHelperActivityType.LoginFailed;
                case "501":
                {
                    return enHelperActivityType.LoginRequired;
                }
            }

            return enHelperActivityType.FileDoesNotExist;
        }

        public AniDBCommand_MarkFileAsDeleted()
        {
            commandType = enAniDBCommandType.MarkFileDeleted;
        }

        public void Init(string hash, long fileSize)
        {
            Hash = hash;
            commandID = "Deleting File: " + hash;

            commandText = "MYLISTADD size=" + fileSize;
            commandText += "&ed2k=" + hash;
            commandText += "&state=" + (int) AniDBFileStatus.Deleted;
            commandText += "&edit=1";
        }
    }
}