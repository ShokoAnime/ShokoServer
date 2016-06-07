using JMMContracts;

namespace JMMServer.Entities
{
    public class CrossRef_AniDB_MAL
    {
        public int CrossRef_AniDB_MALID { get; private set; }
        public int AnimeID { get; set; }
        public int MALID { get; set; }
        public string MALTitle { get; set; }
        public int StartEpisodeType { get; set; }
        public int StartEpisodeNumber { get; set; }
        public int CrossRefSource { get; set; }

        public Contract_CrossRef_AniDB_MAL ToContract()
        {
            var contract = new Contract_CrossRef_AniDB_MAL();

            contract.CrossRef_AniDB_MALID = CrossRef_AniDB_MALID;
            contract.AnimeID = AnimeID;
            contract.MALID = MALID;
            contract.MALTitle = MALTitle;
            contract.StartEpisodeType = StartEpisodeType;
            contract.StartEpisodeNumber = StartEpisodeNumber;
            contract.CrossRefSource = CrossRefSource;

            return contract;
        }

        public Providers.Azure.CrossRef_AniDB_MAL ToContractAzure()
        {
            var contract = new Providers.Azure.CrossRef_AniDB_MAL();

            contract.AnimeID = AnimeID;
            contract.MALID = MALID;
            contract.MALTitle = MALTitle;
            contract.StartEpisodeType = StartEpisodeType;
            contract.StartEpisodeNumber = StartEpisodeNumber;
            contract.CrossRefSource = CrossRefSource;

            contract.AnimeID = AnimeID;
            contract.CrossRefSource = CrossRefSource;
            contract.MALID = MALID;
            contract.MALTitle = MALTitle;
            contract.StartEpisodeNumber = StartEpisodeNumber;
            contract.StartEpisodeType = AnimeID;

            contract.Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                contract.Username = Constants.AnonWebCacheUsername;

            return contract;
        }
    }
}