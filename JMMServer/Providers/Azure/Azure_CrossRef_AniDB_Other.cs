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

        public Contract_CrossRef_AniDB_OtherResult ToContract()
        {
            var contract = new Contract_CrossRef_AniDB_OtherResult();
            contract.AnimeID = AnimeID;
            contract.CrossRefID = CrossRefID;
            return contract;
        }
    }
}