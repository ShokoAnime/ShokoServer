using JMMContracts;

namespace JMMServer.Entities
{
    public class AniDB_Character_Seiyuu
    {
        public int AniDB_Character_SeiyuuID { get; private set; }
        public int CharID { get; set; }
        public int SeiyuuID { get; set; }

        public AniDB_Character_Seiyuu()
        {
        }

        public Contract_AniDB_Character_Seiyuu ToContract()
        {
            Contract_AniDB_Character_Seiyuu contract = new Contract_AniDB_Character_Seiyuu();

            contract.AniDB_Character_SeiyuuID = this.AniDB_Character_SeiyuuID;
            contract.CharID = this.CharID;
            contract.SeiyuuID = this.SeiyuuID;


            return contract;
        }
    }
}