using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_GetMyListFileInfo : AniDBUDPCommand, IAniDBUDPCommand
    {
        public AniDBCommand_GetMyListFileInfo()
        {
            commandType = enAniDBCommandType.GetMyListFileInfo;
        }

        public int FileID { get; set; }

        public Raw_AniDB_MyListFile MyListFile { get; set; }

        public string GetKey()
        {
            return "AniDBCommand_GetMyListFileInfo" + FileID;
        }

        public virtual enHelperActivityType GetStartEventType()
        {
            return enHelperActivityType.GettingMyListFileInfo;
        }

        public virtual enHelperActivityType Process(ref Socket soUDP,
            ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
        {
            ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

            // handle 555 BANNED and 598 - UNKNOWN COMMAND
            if (ResponseCode == 598) return enHelperActivityType.UnknownCommand_598;
            if (ResponseCode == 555) return enHelperActivityType.Banned_555;

            if (errorOccurred) return enHelperActivityType.NoSuchMyListFile;

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetMyListFileInfo.Process: Response: {0}", socketResponse);

            // Process Response
            var sMsgType = socketResponse.Substring(0, 3);


            switch (sMsgType)
            {
                case "221":
                    {
                        MyListFile = new Raw_AniDB_MyListFile(socketResponse);
                        //BaseConfig.MyAnimeLog.Write(myListFile.ToString());
                        return enHelperActivityType.GotMyListFileInfo;
                    }
                case "321":
                    {
                        return enHelperActivityType.NoSuchMyListFile;
                    }
                case "501":
                    {
                        return enHelperActivityType.LoginRequired;
                    }
            }

            return enHelperActivityType.NoSuchMyListFile;
        }

        public void Init(int fileId)
        {
            FileID = fileId;
            commandText = "MYLIST fid=" + FileID;

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetMyListFileInfo.Process: Request: {0}", commandText);

            commandID = FileID.ToString();
        }
    }
}