using System.Net;
using System.Net.Sockets;
using System.Text;
using Shoko.Models.Enums;

namespace AniDBAPI.Commands
{
    public class AniDBCommand_GetEpisodeInfo : AniDBUDPCommand, IAniDBUDPCommand
    {
        private int episodeID = 0;

        public int EpisodeID
        {
            get { return episodeID; }
            set { episodeID = value; }
        }

        private int episodeNumber = 0;

        public int EpisodeNumber
        {
            get { return episodeNumber; }
            set { episodeNumber = value; }
        }

        private int animeID = 0;

        public int AnimeID
        {
            get { return animeID; }
            set { animeID = value; }
        }

        private EpisodeType episodeType;

        public EpisodeType EpisodeType
        {
            get { return episodeType; }
            set { episodeType = value; }
        }

        private Raw_AniDB_Episode episodeInfo = null;

        public Raw_AniDB_Episode EpisodeInfo
        {
            get { return episodeInfo; }
            set { episodeInfo = value; }
        }

        private bool forceRefresh = false;

        public bool ForceRefresh
        {
            get { return forceRefresh; }
            set { forceRefresh = value; }
        }

        string key = "";

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
            string sMsgType = socketResponse.Substring(0, 3);


            switch (sMsgType)
            {
                case "240":
                {
                    // 240 EPISODE INFO
                    // the first 11 characters should be "240 EPISODE"
                    // the rest of the information should be the data list

                    episodeInfo = new Raw_AniDB_Episode(socketResponse, EpisodeSourceType.Episode);
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


        public AniDBCommand_GetEpisodeInfo()
        {
            commandType = enAniDBCommandType.GetEpisodeInfo;
        }

        public void Init(int episodeID, bool force)
        {
            this.episodeID = episodeID;
            this.forceRefresh = force;

            key = "AniDBCommand_GetEpisodeInfo_" + EpisodeID.ToString();
            commandText = "EPISODE eid=" + episodeID.ToString();

            commandID = episodeID.ToString();
        }

        public void Init(int animeID, int episodeNumber, EpisodeType epType)
        {
            this.episodeNumber = episodeNumber;
            this.animeID = animeID;
            this.episodeType = epType;

            string epNumberFormatted = episodeNumber.ToString();

            switch (epType)
            {
                case EpisodeType.Credits:
                    epNumberFormatted = "C" + episodeNumber.ToString();
                    break;
                case EpisodeType.Special:
                    epNumberFormatted = "S" + episodeNumber.ToString();
                    break;
                case EpisodeType.Other:
                    epNumberFormatted = "0" + episodeNumber.ToString();
                    break;
                case EpisodeType.Trailer:
                    epNumberFormatted = "T" + episodeNumber.ToString();
                    break;
                case EpisodeType.Parody:
                    epNumberFormatted = "P" + episodeNumber.ToString();
                    break;
            }

            key = "AniDBCommand_GetEpisodeInfo_" + animeID.ToString() + "_" + epNumberFormatted;
            commandText = string.Format("EPISODE aid={0}&epno={1}", animeID, epNumberFormatted);

            //BaseConfig.MyAnimeLog.Write("AniDBCommand_GetEpisodeInfo.Process: Request: {0}", commandText);

            commandID = animeID.ToString();
        }
    }
}