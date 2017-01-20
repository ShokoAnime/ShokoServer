using Shoko.Models;
using Shoko.Models.Server;

namespace Shoko.Models.Azure
{
    public class Azure_CrossRef_AniDB_TvDB : CrossRef_AniDB_TvDBV2
    {
        public string AnimeName { get; set; }
        public string Username { get; set; }
        public int IsAdminApproved { get; set; }
        public long DateSubmitted { get; set; }
     
    }
}