using Shoko.Models.Client;

namespace Shoko.Models.Azure
{
    public class Azure_CrossRef_AniDB_MAL : CL_CrossRef_AniDB_MAL_Response
    {
        public long DateSubmitted { get; set; }
        public string Self { get; set; }
    }
}