using JMMContracts;

namespace JMMServer.Providers.Azure
{
    public class CrossRef_AniDB_Other
    {
        public int CrossRef_AniDB_OtherID { get; set; }
        public int AnimeID { get; set; }
        public string CrossRefID { get; set; }
        public int CrossRefSource { get; set; }
        public int CrossRefType { get; set; }
        public string Username { get; set; }
        public int IsAdminApproved { get; set; }
        public long DateSubmitted { get; set; }

        public CrossRef_AniDB_Other()
        {
        }

        public Contract_CrossRef_AniDB_OtherResult ToContract()
        {
            Contract_CrossRef_AniDB_OtherResult contract = new Contract_CrossRef_AniDB_OtherResult();
            contract.AnimeID = this.AnimeID;
            contract.CrossRefID = this.CrossRefID;
            return contract;
        }
    }
}