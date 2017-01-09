using System.Globalization;
using Shoko.Models.Client;
using Shoko.Models.Server;

namespace Shoko.Models.Azure
{
    public class Azure_CrossRef_AniDB_MAL : CL_CrossRef_AniDB_MAL_Response
    {
        public long DateSubmitted { get; set; }
        public string Self { get; set; }       
    }
}