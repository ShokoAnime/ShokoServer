using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_GetAnimeDescription : AniDBUDPCommand, IAniDBUDPCommand
    {
        public AniDBCommand_GetAnimeDescription()
        {
            commandType = enAniDBCommandType.GetAnimeDescription;
        }

        public int AnimeID { get; set; }

        public Raw_AniDB_AnimeDesc AnimeDesc { get; set; }

        public string GetKey()
        {
            return "AniDBCommand_GetAnimeDescription" + AnimeID;
        }

        public virtual enHelperActivityType GetStartEventType()
        {
            return enHelperActivityType.GettingAnimeDesc;
        }

        public virtual enHelperActivityType Process(ref Socket soUDP,
            ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
        {
            ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

            // handle 555 BANNED and 598 - UNKNOWN COMMAND
            if (ResponseCode == 598) return enHelperActivityType.UnknownCommand_598;
            if (ResponseCode == 555) return enHelperActivityType.Banned_555;

            if (errorOccurred) return enHelperActivityType.NoSuchAnime;

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetAnimeDescription.Process: Response: {0}", socketResponse);

            // Process Response
            var sMsgType = socketResponse.Substring(0, 3);


            switch (sMsgType)
            {
                case "233":
                    {
                        // 233 ANIMEDESC
                        // the first 11 characters should be "240 EPISODE"
                        // the rest of the information should be the data list

                        AnimeDesc = new Raw_AniDB_AnimeDesc(socketResponse);
                        return enHelperActivityType.GotAnimeDesc;
                    }
                case "330":
                    {
                        return enHelperActivityType.NoSuchAnime;
                    }
                case "333": // no such description
                    {
                        return enHelperActivityType.NoSuchAnime;
                    }
                case "501":
                    {
                        return enHelperActivityType.LoginRequired;
                    }
            }

            return enHelperActivityType.FileDoesNotExist;
        }

        public void Init(int animeID)
        {
            AnimeID = animeID;
            commandText = "ANIMEDESC aid=" + animeID;
            commandText += "&part=0";

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetAnimeDescription.Process: Request: {0}", commandText);

            commandID = animeID.ToString();
        }
    }
}