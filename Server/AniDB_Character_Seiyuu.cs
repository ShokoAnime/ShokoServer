namespace Shoko.Models.Server
{
    public class AniDB_Character_Seiyuu
    {
        public AniDB_Character_Seiyuu() //Empty Constructor for nhibernate
        {

        }
        #region Server DB columns

        public int AniDB_Character_SeiyuuID { get; private set; }
        public int CharID { get; set; }
        public int SeiyuuID { get; set; }

        #endregion
    }
}