using JMMContracts;

namespace JMMServer.Entities
{
    public class CrossRef_AniDB_TvDB_Episode
    {
        public int CrossRef_AniDB_TvDB_EpisodeID { get; private set; }
        public int AnimeID { get; set; }
        public int AniDBEpisodeID { get; set; }
        public int TvDBEpisodeID { get; set; }

        public Contract_CrossRef_AniDB_TvDB_Episode ToContract()
        {
            var contract = new Contract_CrossRef_AniDB_TvDB_Episode();
            contract.AnimeID = AnimeID;
            contract.AniDBEpisodeID = AniDBEpisodeID;
            contract.CrossRef_AniDB_TvDB_EpisodeID = CrossRef_AniDB_TvDB_EpisodeID;
            contract.TvDBEpisodeID = TvDBEpisodeID;
            return contract;
        }
    }
}