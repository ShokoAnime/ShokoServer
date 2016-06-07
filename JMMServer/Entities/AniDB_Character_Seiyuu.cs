using JMMContracts;

namespace JMMServer.Entities
{
    public class AniDB_Character_Seiyuu
    {
        public int AniDB_Character_SeiyuuID { get; private set; }
        public int CharID { get; set; }
        public int SeiyuuID { get; set; }

        public Contract_AniDB_Character_Seiyuu ToContract()
        {
            var contract = new Contract_AniDB_Character_Seiyuu();

            contract.AniDB_Character_SeiyuuID = AniDB_Character_SeiyuuID;
            contract.CharID = CharID;
            contract.SeiyuuID = SeiyuuID;


            return contract;
        }
    }
}