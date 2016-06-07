using JMMContracts;

namespace JMMServer.WebCache
{
    public class CrossRef_AniDB_TvDBResult
    {
        // default constructor

        public int AnimeID { get; set; }
        public int TvDBID { get; set; }
        public int TvDBSeasonNumber { get; set; }
        public int AdminApproved { get; set; }
        public string SeriesName { get; set; }

        public Contract_CrossRef_AniDB_TvDBResult ToContract()
        {
            var contract = new Contract_CrossRef_AniDB_TvDBResult();
            contract.AnimeID = AnimeID;
            contract.TvDBID = TvDBID;
            contract.TvDBSeasonNumber = TvDBSeasonNumber;
            contract.AdminApproved = AdminApproved;
            contract.SeriesName = SeriesName;
            return contract;
        }
    }
}