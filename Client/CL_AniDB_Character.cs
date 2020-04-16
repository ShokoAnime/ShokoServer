using System;
using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_AniDB_Character : AniDB_Character, ICloneable
    {
        // from AniDB_Anime_Character
        public string CharType { get; set; }

        public AniDB_Seiyuu Seiyuu { get; set; }

        public CL_AniDB_Character()
        {
        }

        public CL_AniDB_Character(AniDB_Character obj)
        {
            AniDB_CharacterID = obj.AniDB_CharacterID;
            CharID = obj.CharID;
            PicName = obj.PicName;
            CreatorListRaw = obj.CreatorListRaw;
            CharName = obj.CharName;
            CharKanjiName = obj.CharKanjiName;
            CharDescription = obj.CharDescription;
        }

        public new object Clone()
        {
            var character = new CL_AniDB_Character(this)
            {
                Seiyuu = (AniDB_Seiyuu) Seiyuu?.Clone(), CharType = CharType
            };

            return character;
        }
    }
}
