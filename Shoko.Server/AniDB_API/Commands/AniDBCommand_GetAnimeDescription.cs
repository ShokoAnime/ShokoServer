using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_GetAnimeDescription : AniDBUDPCommand, IAniDBUDPCommand
    {
        private int animeID;

        public int AnimeID
        {
            get { return animeID; }
            set { animeID = value; }
        }

        public string GetKey()
        {
            return "AniDBCommand_GetAnimeDescription" + AnimeID.ToString();
        }

        private Raw_AniDB_AnimeDesc animeDesc;

        public Raw_AniDB_AnimeDesc AnimeDesc
        {
            get { return animeDesc; }
            set { animeDesc = value; }
        }

        public virtual AniDBUDPResponseCode GetStartEventType()
        {
            return AniDBUDPResponseCode.GettingAnimeDesc;
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


            if (errorOccurred) return AniDBUDPResponseCode.NoSuchAnime;

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetAnimeDescription.Process: Response: {0}", socketResponse);

            // Process Response
            string sMsgType = socketResponse.Substring(0, 3);


            switch (sMsgType)
            {
                case "233":
                {
                    // 233 ANIMEDESC
                    // the first 11 characters should be "240 EPISODE"
                    // the rest of the information should be the data list

                    animeDesc = new Raw_AniDB_AnimeDesc(socketResponse);
                    return AniDBUDPResponseCode.GotAnimeDesc;
                }
                case "330": return AniDBUDPResponseCode.NoSuchAnime;
                case "333": // no such description
                    return AniDBUDPResponseCode.NoSuchAnime;
                case "501": return AniDBUDPResponseCode.LoginRequired;
            }

            return AniDBUDPResponseCode.FileDoesNotExist;
        }

        public AniDBCommand_GetAnimeDescription()
        {
            commandType = enAniDBCommandType.GetAnimeDescription;
        }

        public void Init(int animeID)
        {
            this.animeID = animeID;
            commandText = "ANIMEDESC aid=" + animeID.ToString();
            commandText += "&part=0";

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetAnimeDescription.Process: Request: {0}", commandText);

            commandID = animeID.ToString();
        }
    }
}