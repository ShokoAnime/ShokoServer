using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_GetMyListFileInfo : AniDBUDPCommand, IAniDBUDPCommand
    {
        private int fileID;

        public int FileID
        {
            get { return fileID; }
            set { fileID = value; }
        }

        public string GetKey()
        {
            return "AniDBCommand_GetMyListFileInfo" + FileID;
        }

        private Raw_AniDB_MyListFile myListFile;

        public Raw_AniDB_MyListFile MyListFile
        {
            get { return myListFile; }
            set { myListFile = value; }
        }

        public virtual AniDBUDPResponseCode GetStartEventType()
        {
            return AniDBUDPResponseCode.GettingMyListFileInfo;
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

            if (errorOccurred) return AniDBUDPResponseCode.NoSuchMyListFile;

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetMyListFileInfo.Process: Response: {0}", socketResponse);

            // Process Response
            string sMsgType = socketResponse.Substring(0, 3);


            switch (sMsgType)
            {
                case "221":
                {
                    myListFile = new Raw_AniDB_MyListFile(socketResponse);
                    //BaseConfig.MyAnimeLog.Write(myListFile.ToString());
                    return AniDBUDPResponseCode.GotMyListFileInfo;
                }
                case "321": return AniDBUDPResponseCode.NoSuchMyListFile;
                case "501": return AniDBUDPResponseCode.LoginRequired;
            }

            return AniDBUDPResponseCode.NoSuchMyListFile;
        }

        public AniDBCommand_GetMyListFileInfo()
        {
            commandType = enAniDBCommandType.GetMyListFileInfo;
        }

        public void Init(int fileId)
        {
            fileID = fileId;
            commandText = "MYLIST fid=" + fileID;

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetMyListFileInfo.Process: Request: {0}", commandText);

            commandID = fileID.ToString();
        }
    }
}