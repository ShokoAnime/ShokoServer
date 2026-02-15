using System.Collections.Generic;

namespace Shoko.Server.API.v1.Models;

public class CL_AniDB_AnimeCrossRefs
{
    public int AnimeID { get; set; }

    // TvDB
    public List<object> CrossRef_AniDB_TvDB { get; set; }
    public List<object> TvDBSeries { get; set; }
    public List<object> TvDBEpisodes { get; set; }
    public List<object> TvDBImageFanarts { get; set; }
    public List<object> TvDBImagePosters { get; set; }
    public List<object> TvDBImageWideBanners { get; set; }

    // Trakt
    public List<CL_CrossRef_AniDB_TraktV2> CrossRef_AniDB_Trakt { get; set; }
    public List<CL_Trakt_Show> TraktShows { get; set; }

    // MovieDB
    public CL_CrossRef_AniDB_Other CrossRef_AniDB_MovieDB { get; set; }
    public CL_MovieDB_Movie MovieDBMovie { get; set; }
    public List<CL_MovieDB_Fanart> MovieDBFanarts { get; set; }
    public List<CL_MovieDB_Poster> MovieDBPosters { get; set; }

    // MAL
    public List<CL_CrossRef_AniDB_MAL> CrossRef_AniDB_MAL { get; set; }
}
