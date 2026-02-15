
namespace Shoko.Server.API.v1.Models;

public class CL_CrossRef_AniDB_MAL
{
    public int CrossRef_AniDB_MALID { get; set; }
    public int AnimeID { get; set; }
    public int MALID { get; set; }
    public string MALTitle { get; set; }
    public int StartEpisodeType { get; set; }
    public int StartEpisodeNumber { get; set; }
    public int CrossRefSource { get; set; }
}
