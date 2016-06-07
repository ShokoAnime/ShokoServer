using JMMContracts;

namespace JMMServer.Providers.Azure
{
    public class Azure_AnimeLink
    {
        public int RandomAnimeID { get; set; }
        public int AnimeNeedingApproval { get; set; }

        public override string ToString()
        {
            return string.Format("{0} - {1}", AnimeNeedingApproval, RandomAnimeID);
        }

        public Contract_Azure_AnimeLink ToContract()
        {
            var contract = new Contract_Azure_AnimeLink();

            contract.RandomAnimeID = RandomAnimeID;
            contract.AnimeNeedingApproval = AnimeNeedingApproval;

            return contract;
        }
    }
}