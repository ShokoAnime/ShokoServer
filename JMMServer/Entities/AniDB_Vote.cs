using JMMContracts;

namespace JMMServer.Entities
{
    public class AniDB_Vote
    {
        public int AniDB_VoteID { get; private set; }
        public int EntityID { get; set; }
        public int VoteValue { get; set; }
        public int VoteType { get; set; }

        public Contract_AniDBVote ToContract()
        {
            Contract_AniDBVote contract = new Contract_AniDBVote();

            contract.EntityID = this.EntityID;
            contract.VoteValue = (decimal) this.VoteValue/(decimal) 100;
            contract.VoteType = this.VoteType;

            return contract;
        }
    }
}