using System;

namespace Shoko.Server.API.v1.Models;

public class CL_AniDB_Character : ICloneable
{
    // from AniDB_Anime_Character
    public int AniDB_CharacterID { get; set; }
    public int CharID { get; set; }
    public string PicName { get; set; }
    public string CreatorListRaw { get; set; }
    public string CharName { get; set; }
    public string CharKanjiName { get; set; }
    public string CharDescription { get; set; }
    public string CharType { get; set; }

    public CL_AniDB_Seiyuu Seiyuu { get; set; }

    public object Clone()
    {
        var character = new CL_AniDB_Character()
        {
            AniDB_CharacterID = AniDB_CharacterID,
            CharID = CharID,
            PicName = PicName,
            CreatorListRaw = CreatorListRaw,
            CharName = CharName,
            CharKanjiName = CharKanjiName,
            CharDescription = CharDescription,
            CharType = CharType,
            Seiyuu = (CL_AniDB_Seiyuu)Seiyuu?.Clone(),
        };

        return character;
    }
}
