using JMMContracts;

namespace JMMServer.Providers.Azure
{
    public class CrossRef_AniDB_TvDB
    {
        public int AnimeID { get; set; }
        public string AnimeName { get; set; }
        public int AniDBStartEpisodeType { get; set; }
        public int AniDBStartEpisodeNumber { get; set; }
        public int TvDBID { get; set; }
        public int TvDBSeasonNumber { get; set; }
        public int TvDBStartEpisodeNumber { get; set; }
        public string TvDBTitle { get; set; }
        public int CrossRefSource { get; set; }
        public string Username { get; set; }
        public int IsAdminApproved { get; set; }
        public long DateSubmitted { get; set; }

        public int? CrossRef_AniDB_TvDBId { get; set; }

        public CrossRef_AniDB_TvDB()
        {
        }

        public Contract_Azure_CrossRef_AniDB_TvDB ToContract()
        {
            Contract_Azure_CrossRef_AniDB_TvDB ret = new Contract_Azure_CrossRef_AniDB_TvDB();

            ret.AnimeID = AnimeID;
            ret.AnimeName = AnimeName;
            ret.AniDBStartEpisodeType = AniDBStartEpisodeType;
            ret.AniDBStartEpisodeNumber = AniDBStartEpisodeNumber;
            ret.TvDBID = TvDBID;
            ret.TvDBSeasonNumber = TvDBSeasonNumber;
            ret.TvDBStartEpisodeNumber = TvDBStartEpisodeNumber;
            ret.TvDBTitle = TvDBTitle;
            ret.CrossRefSource = CrossRefSource;
            ret.Username = Username;
            ret.IsAdminApproved = IsAdminApproved;
            ret.DateSubmitted = DateSubmitted;

            ret.CrossRef_AniDB_TvDBId = CrossRef_AniDB_TvDBId;

            return ret;
        }
    }
}