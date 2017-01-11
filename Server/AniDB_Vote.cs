using Shoko.Models;

namespace Shoko.Models.Server
{
    public class AniDB_Vote
    {
        public AniDB_Vote()
        {
        }

        public int AniDB_VoteID { get; private set; }
        public int EntityID { get; set; }
        public int VoteValue { get; set; }  //WARNING FIX IN CLIENT THE VALUE SHOULD BE DIVIDED BY 100 in the Clients
        public int VoteType { get; set; }

    }
}