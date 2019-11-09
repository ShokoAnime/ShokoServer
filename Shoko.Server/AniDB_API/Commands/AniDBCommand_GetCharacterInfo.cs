using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_GetCharacterInfo : AniDBUDPCommand, IAniDBUDPCommand
    {
        private int charID = 0;

        public int CharID
        {
            get { return charID; }
            set { charID = value; }
        }

        private Raw_AniDB_Character charInfo = null;

        public Raw_AniDB_Character CharInfo
        {
            get { return charInfo; }
            set { charInfo = value; }
        }

        private bool forceRefresh = false;

        public bool ForceRefresh
        {
            get { return forceRefresh; }
            set { forceRefresh = value; }
        }

        public string GetKey()
        {
            return "AniDBCommand_GetCharacterInfo" + CharID.ToString();
        }

        public virtual AniDBUDPResponseCode GetStartEventType()
        {
            return AniDBUDPResponseCode.GettingCharInfo;
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


            if (errorOccurred) return AniDBUDPResponseCode.NoSuchChar;

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetCharacterInfo.Process: Response: {0}", socketResponse);

            // Process Response
            string sMsgType = socketResponse.Substring(0, 3);


            switch (sMsgType)
            {
                case "235":
                {
                    // 235 CHARACTER INFO
                    // the first 11 characters should be "235 CHARACTER"
                    // the rest of the information should be the data list

                    charInfo = new Raw_AniDB_Character(socketResponse);
                    return AniDBUDPResponseCode.GotCharInfo;


                    // Response: 235 CHARACTER 99297|6267|25|539|5|01|The Girl Returns|Shoujo Kikan|????|1238976000
                }
                case "335": return AniDBUDPResponseCode.NoSuchChar;
                case "501": return AniDBUDPResponseCode.LoginRequired;
            }

            return AniDBUDPResponseCode.NoSuchChar;
        }


        public AniDBCommand_GetCharacterInfo()
        {
            commandType = enAniDBCommandType.GetCharInfo;
        }

        public void Init(int charID, bool force)
        {
            this.charID = charID;
            this.forceRefresh = force;
            commandText = "CHARACTER charid=" + charID.ToString();

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetCharacterInfo.Process: Request: {0}", commandText);

            commandID = charID.ToString();
        }
    }
}