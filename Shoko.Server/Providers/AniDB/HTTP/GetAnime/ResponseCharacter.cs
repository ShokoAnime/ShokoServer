using System;
using System.Collections.Generic;

namespace Shoko.Server.Providers.AniDB.HTTP.GetAnime;

public class ResponseCharacter
{
    public int CharacterID { get; set; }
    public int AnimeID { get; set; }
    public string PicName { get; set; }
    public string CharacterName { get; set; }
    public string CharacterKanjiName { get; set; }
    public string CharacterDescription { get; set; }
    public string CharacterAppearanceType { get; set; }
    public string CharacterType { get; set; }
    public string Gender { get; set; }
    public List<ResponseSeiyuu> Seiyuus { get; set; }
    public DateTime LastUpdated { get; set; }
}
