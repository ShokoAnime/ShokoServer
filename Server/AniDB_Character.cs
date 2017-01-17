using Shoko.Models.Client;

namespace Shoko.Models.Server
{
    public class AniDB_Character
    {
        #region Server DB columns

        public int AniDB_CharacterID { get; set; }
        public int CharID { get; set; }
        public string PicName { get; set; }
        public string CreatorListRaw { get; set; }
        public string CharName { get; set; }
        public string CharKanjiName { get; set; }
        public string CharDescription { get; set; }

        #endregion

        public AniDB_Character() //Empty Constructor for nhibernate
        {

        }
    }
}