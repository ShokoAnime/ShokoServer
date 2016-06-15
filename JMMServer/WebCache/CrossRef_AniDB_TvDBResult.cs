using JMMContracts;

namespace JMMServer.WebCache
{
    public class CrossRef_AniDB_TvDBResult
    {
        public int AnimeID { get; set; }
        public int TvDBID { get; set; }
        public int TvDBSeasonNumber { get; set; }
        public int AdminApproved { get; set; }
        public string SeriesName { get; set; }

        // default constructor
        public CrossRef_AniDB_TvDBResult()
        {
        }

        public Contract_CrossRef_AniDB_TvDBResult ToContract()
        {
            Contract_CrossRef_AniDB_TvDBResult contract = new Contract_CrossRef_AniDB_TvDBResult();
            contract.AnimeID = this.AnimeID;
            contract.TvDBID = this.TvDBID;
            contract.TvDBSeasonNumber = this.TvDBSeasonNumber;
            contract.AdminApproved = this.AdminApproved;
            contract.SeriesName = this.SeriesName;
            return contract;
        }
    }
}