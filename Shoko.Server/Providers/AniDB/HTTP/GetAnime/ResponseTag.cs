using System;

namespace Shoko.Server.Providers.AniDB.HTTP.GetAnime;

public class ResponseTag
{
    public int AnimeID { get; set; }
    public int TagID { get; set; }
    public int? ParentTagID { get; set; }
    public bool Verified { get; set; }
    public bool LocalSpoiler { get; set; }
    public bool GlobalSpoiler { get; set; }
    public string TagName { get; set; }
    public string TagDescription { get; set; }
    public int Weight { get; set; }
    public DateTime LastUpdated { get; set; }
}
