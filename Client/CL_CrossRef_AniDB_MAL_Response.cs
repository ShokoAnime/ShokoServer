using Shoko.Models.Azure;

namespace Shoko.Models.Client
{
    public class CL_CrossRef_AniDB_MAL_Response : Azure_CrossRef_AniDB_MAL_Request
    {
        public int IsAdminApproved { get; set; }
    }
}