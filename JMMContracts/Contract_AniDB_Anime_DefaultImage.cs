using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_AniDB_Anime_DefaultImage
	{
		public int AniDB_Anime_DefaultImageID { get; set; }
		public int AnimeID { get; set; }
		public int ImageParentID { get; set; }
		public int ImageParentType { get; set; }
		public int ImageType { get; set; }

		public Contract_MovieDB_Poster MoviePoster { get; set; }
		public Contract_MovieDB_Fanart MovieFanart { get; set; }

		public Contract_TvDB_ImagePoster TVPoster { get; set; }
		public Contract_TvDB_ImageFanart TVFanart { get; set; }
		public Contract_TvDB_ImageWideBanner TVWideBanner { get; set; }

		public Contract_Trakt_ImagePoster TraktPoster { get; set; }
		public Contract_Trakt_ImageFanart TraktFanart { get; set; }
	}
}
