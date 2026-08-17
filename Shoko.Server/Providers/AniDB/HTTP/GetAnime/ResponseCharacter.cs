using System;
using System.Collections.Generic;

namespace Shoko.Server.Providers.AniDB.HTTP.GetAnime;

public class ResponseCharacter
{
    public int CharacterID { get; set; }
    public int AnimeID { get; set; }
    public string PicName { get; set; } = null!;
    public string CharacterName { get; set; } = null!;
    public string? CharacterKanjiName { get; set; }
    public string CharacterDescription { get; set; } = null!;
    public string CharacterAppearanceType { get; set; } = null!;
    public string CharacterType { get; set; } = null!;
    public string Gender { get; set; } = null!;
    public List<ResponseSeiyuu> Seiyuus { get; set; } = null!;
    public DateTime LastUpdated { get; set; }
}
