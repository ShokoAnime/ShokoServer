using System.Globalization;
using JMMContracts;

namespace JMMServer.Providers.Azure
{
    public class CrossRef_AniDB_MAL
    {
        public int CrossRef_AniDB_MALId { get; set; }
        public int AnimeID { get; set; }
        public int MALID { get; set; }
        public int CrossRefSource { get; set; }
        public string Username { get; set; }
        public string MALTitle { get; set; }
        public int StartEpisodeType { get; set; }
        public int StartEpisodeNumber { get; set; }
        public int IsAdminApproved { get; set; }
        public long DateSubmitted { get; set; }

        public string Self
        {
            get
            {
                return string.Format(CultureInfo.CurrentCulture, "api/crossRef_anidb_mal/{0}", CrossRef_AniDB_MALId);
            }
            set { }
        }

        public Contract_CrossRef_AniDB_MALResult ToContract()
        {
            var contract = new Contract_CrossRef_AniDB_MALResult();
            contract.AnimeID = AnimeID;
            contract.MALID = MALID;
            contract.CrossRefSource = CrossRefSource;
            contract.MALTitle = MALTitle;
            contract.StartEpisodeType = StartEpisodeType;
            contract.StartEpisodeNumber = StartEpisodeNumber;
            contract.IsAdminApproved = IsAdminApproved;

            return contract;
        }
    }
}