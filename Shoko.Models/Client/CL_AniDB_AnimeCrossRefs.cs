using System.Collections.Generic;
using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_AniDB_AnimeCrossRefs
    {
        public int AnimeID { get; set; }

        // TvDB
        public List<CrossRef_AniDB_TvDBV2> CrossRef_AniDB_TvDB { get; set; }
        public List<TvDB_Series> TvDBSeries { get; set; }
        public List<TvDB_Episode> TvDBEpisodes { get; set; }
        public List<TvDB_ImageFanart> TvDBImageFanarts { get; set; }
        public List<TvDB_ImagePoster> TvDBImagePosters { get; set; }
        public List<TvDB_ImageWideBanner> TvDBImageWideBanners { get; set; }

        // Trakt
        public List<CrossRef_AniDB_TraktV2> CrossRef_AniDB_Trakt { get; set; }
        public List<CL_Trakt_Show> TraktShows { get; set; }

        // MovieDB
        public CrossRef_AniDB_Other CrossRef_AniDB_MovieDB { get; set; }
        public MovieDB_Movie MovieDBMovie { get; set; }
        public List<MovieDB_Fanart> MovieDBFanarts { get; set; }
        public List<MovieDB_Poster> MovieDBPosters { get; set; }

        // MAL
        public List<CrossRef_AniDB_MAL> CrossRef_AniDB_MAL { get; set; }


    }
}