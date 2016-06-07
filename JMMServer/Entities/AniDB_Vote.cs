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
            var contract = new Contract_AniDBVote();

            contract.EntityID = EntityID;
            contract.VoteValue = VoteValue / (decimal)100;
            contract.VoteType = VoteType;

            return contract;
        }
    }
}