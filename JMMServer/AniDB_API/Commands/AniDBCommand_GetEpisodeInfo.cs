using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_GetEpisodeInfo : AniDBUDPCommand, IAniDBUDPCommand
    {
        private string key = "";


        public AniDBCommand_GetEpisodeInfo()
        {
            commandType = enAniDBCommandType.GetEpisodeInfo;
        }

        public int EpisodeID { get; set; }

        public int EpisodeNumber { get; set; }

        public int AnimeID { get; set; }

        public enEpisodeType EpisodeType { get; set; }

        public Raw_AniDB_Episode EpisodeInfo { get; set; }

        public bool ForceRefresh { get; set; }

        public string GetKey()
        {
            return key;
        }

        public virtual enHelperActivityType GetStartEventType()
        {
            return enHelperActivityType.GettingEpisodeInfo;
        }

        public virtual enHelperActivityType Process(ref Socket soUDP,
            ref IPEndPoint remoteIpEndPoint, string sessionID, Encoding enc)
        {
            ProcessCommand(ref soUDP, ref remoteIpEndPoint, sessionID, enc);

            // handle 555 BANNED and 598 - UNKNOWN COMMAND
            if (ResponseCode == 598) return enHelperActivityType.UnknownCommand_598;
            if (ResponseCode == 555) return enHelperActivityType.Banned_555;

            if (errorOccurred) return enHelperActivityType.NoSuchEpisode;

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetEpisodeInfo.Process: Response: {0}", socketResponse);

            // Process Response
            var sMsgType = socketResponse.Substring(0, 3);


            switch (sMsgType)
            {
                case "240":
                    {
                        // 240 EPISODE INFO
                        // the first 11 characters should be "240 EPISODE"
                        // the rest of the information should be the data list

                        EpisodeInfo = new Raw_AniDB_Episode(socketResponse, enEpisodeSourceType.Episode);
                        return enHelperActivityType.GotEpisodeInfo;


                        // Response: 240 EPISODE 99297|6267|25|539|5|01|The Girl Returns|Shoujo Kikan|????|1238976000
                    }
                case "340":
                    {
                        return enHelperActivityType.NoSuchEpisode;
                    }
                case "501":
                    {
                        return enHelperActivityType.LoginRequired;
                    }
            }

            return enHelperActivityType.NoSuchEpisode;
        }

        public void Init(int episodeID, bool force)
        {
            EpisodeID = episodeID;
            ForceRefresh = force;

            key = "AniDBCommand_GetEpisodeInfo_" + EpisodeID;
            commandText = "EPISODE eid=" + episodeID;

            commandID = episodeID.ToString();
        }

        public void Init(int animeID, int episodeNumber, enEpisodeType epType)
        {
            EpisodeNumber = episodeNumber;
            AnimeID = animeID;
            EpisodeType = epType;

            var epNumberFormatted = episodeNumber.ToString();

            switch (epType)
            {
                case enEpisodeType.Credits:
                    epNumberFormatted = "C" + episodeNumber;
                    break;
                case enEpisodeType.Special:
                    epNumberFormatted = "S" + episodeNumber;
                    break;
                case enEpisodeType.Other:
                    epNumberFormatted = "0" + episodeNumber;
                    break;
                case enEpisodeType.Trailer:
                    epNumberFormatted = "T" + episodeNumber;
                    break;
                case enEpisodeType.Parody:
                    epNumberFormatted = "P" + episodeNumber;
                    break;
            }

            key = "AniDBCommand_GetEpisodeInfo_" + animeID + "_" + epNumberFormatted;
            commandText = string.Format("EPISODE aid={0}&epno={1}", animeID, epNumberFormatted);

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetEpisodeInfo.Process: Request: {0}", commandText);

            commandID = animeID.ToString();
        }
    }
}