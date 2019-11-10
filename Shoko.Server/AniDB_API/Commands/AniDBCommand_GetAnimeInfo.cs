using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_GetAnimeInfo : AniDBUDPCommand, IAniDBUDPCommand
    {
        private Raw_AniDB_Anime animeInfo = null; //AniDB_Anime

        public Raw_AniDB_Anime AnimeInfo
        {
            get { return animeInfo; }
            set { animeInfo = value; }
        }

        private int animeID;

        public int AnimeID
        {
            get { return animeID; }
            set { animeID = value; }
        }

        private bool forceRefresh = false;

        public bool ForceRefresh
        {
            get { return forceRefresh; }
            set { forceRefresh = value; }
        }

        public string GetKey()
        {
            return "AniDBCommand_GetAnimeInfo_" + AnimeID.ToString();
        }

        public virtual AniDBUDPResponseCode GetStartEventType()
        {
            return AniDBUDPResponseCode.GettingAnimeInfo;
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

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetAnimeInfo.Process: Response: {0}", socketResponse);

            // Process Response
            string sMsgType = socketResponse.Substring(0, 3);


            switch (sMsgType)
            {
                case "230":
                {
                    // 230 FILE INFO
                    // the first 9 characters should be "230 ANIME "
                    // the rest of the information should be the data list
                    animeInfo = new Raw_AniDB_Anime(socketResponse);
                    return AniDBUDPResponseCode.GotAnimeInfo;
                }
                case "330": return AniDBUDPResponseCode.NoSuchAnime;
                case "501": return AniDBUDPResponseCode.LoginRequired;
            }

            return AniDBUDPResponseCode.FileDoesNotExist;
        }

        public AniDBCommand_GetAnimeInfo()
        {
            commandType = enAniDBCommandType.GetAnimeInfo;
        }

        public void Init(int animeID, bool force)
        {
            /*
			// 1 			int4	aid
			// 2 			int4	episodes
			// 4 			int4	normal ep count 
			// 16 			int4	rating 
			// 32 			int4	vote count 
			// 64 			int4	temp rating 
			// 128 			int4	temp vote count 
			// 1024 		int4	air date 
			// 2048 		int4	end date 
			// 65536 		str		url 
			// 131072 		str		picname 
			// 262144 		str		year 
			// 524288 		str		type 
			// 1048576 		str		romaji name 
			// 4194304 		str		english name 
			// 67108864		str		category list 
			// 1073741824 	str		award list 

			commandText = "ANIME aid=" + animeID.ToString();
			commandText += "&acode=1147079927";

			BaseConfig.MyAnimeLog.Write("AniDBCommand_GetAnimeInfo.Process: Request: {0}", commandText);

            commandID = animeID.ToString();
			*/

            this.animeID = animeID;
            this.forceRefresh = force;


            //v0.3
            int aByte1 = 191; // amask - byte1
            int aByte2 = 252; // amask - byte2 old 188 new 252 Added Kanji Name
            int aByte3 = 255; // amask - byte3
            int aByte4 = 255; // amask - byte4 old 252 new 255 Added Award List and 18+ Restricted
            int aByte5 = 241;
            // amask - byte5 old 0 new 241 (Added AnimePlanetID/ANN ID/AllCinema ID/AnimeNfo ID/LastUpdate
            int aByte6 = 136; // amask - byte6


            commandID = animeID.ToString();

            commandText = "ANIME aid=" + animeID.ToString();
            commandText += string.Format("&amask={0}{1}{2}{3}{4}{5}", aByte1.ToString("X").PadLeft(2, '0'),
                aByte2.ToString("X").PadLeft(2, '0'),
                aByte3.ToString("X").PadLeft(2, '0'), aByte4.ToString("X").PadLeft(2, '0'),
                aByte5.ToString("X").PadLeft(2, '0'),
                aByte6.ToString("X").PadLeft(2, '0'));

            //BaseConfig.MyAnimeLog.Write(commandText);
        }
    }
}