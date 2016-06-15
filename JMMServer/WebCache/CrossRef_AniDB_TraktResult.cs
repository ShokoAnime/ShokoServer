using JMMContracts;

namespace JMMServer.WebCache
{
    public class CrossRef_AniDB_TraktResult
    {
        public int AnimeID { get; set; }
        public string TraktID { get; set; }
        public int TraktSeasonNumber { get; set; }
        public int AdminApproved { get; set; }
        public string ShowName { get; set; }

        // default constructor
        public CrossRef_AniDB_TraktResult()
        {
        }

        public Contract_CrossRef_AniDB_TraktResult ToContract()
        {
            Contract_CrossRef_AniDB_TraktResult contract = new Contract_CrossRef_AniDB_TraktResult();

            contract.AnimeID = AnimeID;
            contract.TraktID = TraktID;
            contract.TraktSeasonNumber = TraktSeasonNumber;
            contract.AdminApproved = AdminApproved;
            contract.ShowName = ShowName;

            return contract;
        }
    }
}