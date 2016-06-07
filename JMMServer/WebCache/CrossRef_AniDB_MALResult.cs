using JMMContracts;

namespace JMMServer.WebCache
{
    public class CrossRef_AniDB_MALResult
    {
        // default constructor

        public int AnimeID { get; set; }
        public int MALID { get; set; }
        public int CrossRefSource { get; set; }
        public string MALTitle { get; set; }
        public int StartEpisodeType { get; set; }
        public int StartEpisodeNumber { get; set; }

        public Contract_CrossRef_AniDB_MALResult ToContract()
        {
            var contract = new Contract_CrossRef_AniDB_MALResult();
            contract.AnimeID = AnimeID;
            contract.MALID = MALID;
            contract.CrossRefSource = CrossRefSource;
            contract.MALTitle = MALTitle;
            contract.StartEpisodeType = StartEpisodeType;
            contract.StartEpisodeNumber = StartEpisodeNumber;
            return contract;
        }
    }
}