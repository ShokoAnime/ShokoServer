using Shoko.Models.Enums;

namespace Shoko.Server.Providers.AniDB.HTTP.GetAnime;

public class ResponseResource
{
    public int AnimeID { get; set; }
    public AniDB_ResourceLinkType ResourceType { get; set; }
    public string ResourceID { get; set; }
}
