using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_AniDB_GroupStatus : AniDB_GroupStatus
    {
        public bool UserCollecting { get; set; }
        public int FileCount { get; set; }
    }
}
