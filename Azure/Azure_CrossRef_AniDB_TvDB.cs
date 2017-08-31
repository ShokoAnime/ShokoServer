using Shoko.Models.Server;

namespace Shoko.Models.Azure
{
    public class Azure_CrossRef_AniDB_TvDB : CrossRef_AniDB_TvDBV2
    {
        // hacky soltuion to ensure proper deserialize from webcache
        public int CrossRef_AniDB_TvDBId
        {
            get => CrossRef_AniDB_TvDBV2ID;
            set => CrossRef_AniDB_TvDBV2ID = value;
        }

        public string AnimeName { get; set; }
        public string Username { get; set; }
        public int IsAdminApproved { get; set; }
        public long DateSubmitted { get; set; }
    }
}