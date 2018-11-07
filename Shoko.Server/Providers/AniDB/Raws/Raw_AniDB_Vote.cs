using Shoko.Models.Enums;

namespace Shoko.Server.Providers.AniDB.Raws
{
    public class Raw_AniDB_Vote : XMLBase
    {
        public int EntityID { get; set; }

        public int EpisodeNumber { get; set; }

        public int EpisodeType { get; set; }

        public int VoteValue { get; set; }

        public int VoteType { get; set; }

        public Raw_AniDB_Vote()
        {
            EpisodeNumber = -1;
            EpisodeType = 1;
            VoteType = 1;
        }


        // constructor
        // sRecMessage is the message received from ANIDB file info command


        public void ProcessVoteFoundAnime(string sRecMessage, int animeID, AniDBVoteType vtype)
        {
            // remove the header info
            string[] sDetails = sRecMessage.Substring(15).Split('|');

            // 261 VOTE FOUNDCode Geass Hangyaku no Lelouch|900|1|4521


            // 261 VOTE FOUND
            // 0. Code Geass Hangyaku no Lelouch
            // 1. 900 ** vote value
            // 2. 1 ** vote type
            // 3. 4521 ** animeid

            this.EntityID = animeID;
            this.EpisodeNumber = -1;
            this.VoteValue = int.Parse(sDetails[1].Trim());
            this.VoteType = (int) vtype;
            this.EpisodeType = (int) Shoko.Models.Enums.EpisodeType.Episode;
        }

        public void ProcessVoteFoundEpisode(string sRecMessage, int animeID, int epno, EpisodeType epType)
        {
            // remove the header info
            string[] sDetails = sRecMessage.Substring(15).Split('|');

            //261 VOTE FOUNDThe Day a New Demon Was Born|700|1|63091

            // 261 VOTE FOUND
            // 0. The Day a New Demon Was Born
            // 1. 700 ** vote value
            // 2. 1 ** ???
            // 3. 63091 ** episodeid

            this.EntityID = animeID;
            this.EpisodeNumber = epno;
            this.VoteValue = int.Parse(sDetails[1].Trim());
            this.VoteType = (int) AniDBVoteType.Episode;
            this.EpisodeType = (int) epType;
        }

        public override string ToString()
        {
            return
                string.Format(
                    "AniDB_Vote:: entityID: {0} | episodeNumber: {1} | episodeType: {2} |  voteValue: {3} | voteType: {4}",
                    EntityID, EpisodeNumber, EpisodeType, VoteValue, VoteType);
        }
    }
}