using System;

namespace Shoko.Models.Server
{
    public class AniDB_Vote : ICloneable
    {
        public AniDB_Vote()
        {
        }

        public int AniDB_VoteID { get; set; }
        public int EntityID { get; set; }
        public int VoteValue { get; set; }  //WARNING FIX IN CLIENT THE VALUE SHOULD BE DIVIDED BY 100 in the Clients
        public int VoteType { get; set; }

        public object Clone()
        {
            return new AniDB_Vote
            {
                AniDB_VoteID = AniDB_VoteID,
                EntityID = EntityID,
                VoteValue = VoteValue,
                VoteType = VoteType
            };
        }
    }
}
