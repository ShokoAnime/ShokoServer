using System.Collections.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.Info;

public class ResponseGetAnime
{
    public int AnimeID { get; set; }
    public List<Relation> Relations { get; set; } = [];

    public class Relation
    {
        public int RelatedAnimeID { get; set; }
        public int RawType { get; set; }
    }
}
