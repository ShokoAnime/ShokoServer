using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMContracts;

namespace JMMServer.Providers.Azure
{
    public class CrossRef_AniDB_Trakt
    {
        public int AnimeID { get; set; }
        public string AnimeName { get; set; }
        public int AniDBStartEpisodeType { get; set; }
        public int AniDBStartEpisodeNumber { get; set; }
        public string TraktID { get; set; }
        public int TraktSeasonNumber { get; set; }
        public int TraktStartEpisodeNumber { get; set; }
        public string TraktTitle { get; set; }
        public int CrossRefSource { get; set; }
        public string Username { get; set; }
        public int IsAdminApproved { get; set; }
        public long DateSubmitted { get; set; }

        public int? CrossRef_AniDB_TraktId { get; set; }

        public CrossRef_AniDB_Trakt()
		{
		}

        public Contract_Azure_CrossRef_AniDB_Trakt ToContract()
        {
            Contract_Azure_CrossRef_AniDB_Trakt ret = new Contract_Azure_CrossRef_AniDB_Trakt();

            ret.CrossRef_AniDB_TraktId = CrossRef_AniDB_TraktId;
            ret.AnimeID = AnimeID;
            ret.AnimeName = AnimeName;
            ret.AniDBStartEpisodeType = AniDBStartEpisodeType;
            ret.AniDBStartEpisodeNumber = AniDBStartEpisodeNumber;
            ret.TraktID = TraktID;
            ret.TraktSeasonNumber = TraktSeasonNumber;
            ret.TraktStartEpisodeNumber = TraktStartEpisodeNumber;
            ret.TraktTitle = TraktTitle;
            ret.CrossRefSource = CrossRefSource;
            ret.Username = Username;
            ret.IsAdminApproved = IsAdminApproved;
            ret.DateSubmitted = DateSubmitted;

            return ret;
        }
    }
}
