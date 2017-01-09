using System.Collections.Generic;
using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_AniDB_AnimeCrossRefs
    {
        public int AnimeID { get; set; }

        // TvDB
        public List<CrossRef_AniDB_TvDBV2> CrossRef_AniDB_TvDB { get; set; }
        public List<Contract_TvDB_Series> TvDBSeries { get; set; }
        public List<Contract_TvDB_Episode> TvDBEpisodes { get; set; }
        public List<Contract_TvDB_ImageFanart> TvDBImageFanarts { get; set; }
        public List<Contract_TvDB_ImagePoster> TvDBImagePosters { get; set; }
        public List<Contract_TvDB_ImageWideBanner> TvDBImageWideBanners { get; set; }

        // Trakt
        public List<CrossRef_AniDB_TraktV2> CrossRef_AniDB_Trakt { get; set; }
        public List<Contract_Trakt_Show> TraktShows { get; set; }
        public List<Contract_Trakt_ImageFanart> TraktImageFanarts { get; set; }
        public List<Contract_Trakt_ImagePoster> TraktImagePosters { get; set; }

        // MovieDB
        public CrossRef_AniDB_Other CrossRef_AniDB_MovieDB { get; set; }
        public Contract_MovieDB_Movie MovieDBMovie { get; set; }
        public List<Contract_MovieDB_Fanart> MovieDBFanarts { get; set; }
        public List<Contract_MovieDB_Poster> MovieDBPosters { get; set; }

        // MAL
        public List<CrossRef_AniDB_MAL> CrossRef_AniDB_MAL { get; set; }


    }
}