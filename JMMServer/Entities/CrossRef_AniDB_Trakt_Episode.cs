using JMMContracts;

namespace JMMServer.Entities
{
    public class CrossRef_AniDB_Trakt_Episode
    {
        public int CrossRef_AniDB_Trakt_EpisodeID { get; private set; }
        public int AnimeID { get; set; }
        public int AniDBEpisodeID { get; set; }
        public string TraktID { get; set; }
        public int Season { get; set; }
        public int EpisodeNumber { get; set; }

        public Contract_CrossRef_AniDB_Trakt_Episode ToContract()
        {
            var contract = new Contract_CrossRef_AniDB_Trakt_Episode();
            contract.AnimeID = AnimeID;
            contract.AniDBEpisodeID = AniDBEpisodeID;
            contract.CrossRef_AniDB_Trakt_EpisodeID = CrossRef_AniDB_Trakt_EpisodeID;
            contract.TraktID = TraktID;
            contract.Season = Season;
            contract.EpisodeNumber = EpisodeNumber;

            return contract;
        }
    }
}