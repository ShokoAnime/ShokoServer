using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_AniDB_Anime_DefaultImage : AniDB_Anime_DefaultImage
    {
        public Contract_MovieDB_Poster MoviePoster { get; set; }
        public Contract_MovieDB_Fanart MovieFanart { get; set; }

        public Contract_TvDB_ImagePoster TVPoster { get; set; }
        public Contract_TvDB_ImageFanart TVFanart { get; set; }
        public Contract_TvDB_ImageWideBanner TVWideBanner { get; set; }

        public Contract_Trakt_ImagePoster TraktPoster { get; set; }
        public Contract_Trakt_ImageFanart TraktFanart { get; set; }
    }
}
