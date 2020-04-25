using System;

namespace Shoko.Models.Server
{
    public class AniDB_Character : ICloneable
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

        public object Clone()
        {
            return new AniDB_Character
            {
                AniDB_CharacterID = AniDB_CharacterID,
                CharID = CharID,
                PicName = PicName,
                CreatorListRaw = CreatorListRaw,
                CharName = CharName,
                CharKanjiName = CharKanjiName,
                CharDescription = CharDescription
            };
        }
    }
}
