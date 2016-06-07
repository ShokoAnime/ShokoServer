namespace AniDBAPI
{
    public class Raw_AniDB_Vote : XMLBase
    {
        public Raw_AniDB_Vote()
        {
            EpisodeNumber = -1;
            EpisodeType = 1;
            VoteType = 1;
        }

        public int EntityID { get; set; }

        public int EpisodeNumber { get; set; }

        public int EpisodeType { get; set; }

        public int VoteValue { get; set; }

        public int VoteType { get; set; }


        // constructor
        // sRecMessage is the message received from ANIDB file info command


        public void ProcessVoteFoundAnime(string sRecMessage, int animeID, enAniDBVoteType vtype)
        {
            // remove the header info
            var sDetails = sRecMessage.Substring(15).Split('|');

            // 261 VOTE FOUNDCode Geass Hangyaku no Lelouch|900|1|4521


            // 261 VOTE FOUND
            // 0. Code Geass Hangyaku no Lelouch
            // 1. 900 ** vote value
            // 2. 1 ** vote type
            // 3. 4521 ** animeid

            EntityID = animeID;
            EpisodeNumber = -1;
            VoteValue = int.Parse(sDetails[1].Trim());
            VoteType = (int)vtype;
            EpisodeType = (int)enEpisodeType.Episode;
        }

        public void ProcessVoteFoundEpisode(string sRecMessage, int animeID, int epno, enEpisodeType epType)
        {
            // remove the header info
            var sDetails = sRecMessage.Substring(15).Split('|');

            //261 VOTE FOUNDThe Day a New Demon Was Born|700|1|63091

            // 261 VOTE FOUND
            // 0. The Day a New Demon Was Born
            // 1. 700 ** vote value
            // 2. 1 ** ???
            // 3. 63091 ** episodeid

            EntityID = animeID;
            EpisodeNumber = epno;
            VoteValue = int.Parse(sDetails[1].Trim());
            VoteType = (int)enAniDBVoteType.Episode;
            EpisodeType = (int)epType;
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