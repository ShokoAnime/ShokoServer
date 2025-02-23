using Shoko.Models.Azure;

namespace Shoko.Models.Client
{
    public class CL_CrossRef_AniDB_Other_Response : Azure_CrossRef_AniDB_Other_Request
    {
        public int IsAdminApproved { get; set; }
    }
}