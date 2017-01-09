using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_AniDB_Anime : AniDB_Anime
    {
        public CL_AniDB_Anime_DefaultImage DefaultImagePoster { get; set; }
        public CL_AniDB_Anime_DefaultImage DefaultImageFanart { get; set; }
        public CL_AniDB_Anime_DefaultImage DefaultImageWideBanner { get; set; }
        public List<CL_AniDB_Character> Characters { get; set; }
        public List<CL_AniDB_Anime_DefaultImage> Fanarts { get; set; }
        public List<CL_AniDB_Anime_DefaultImage> Banners { get; set; }
        public string FormattedTitle { get; set; }
   }
}
