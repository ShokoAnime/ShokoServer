using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_AniDB_AnimeCrossRefs
	{
		public int AnimeID { get; set; }

		// TvDB
		public List<Contract_CrossRef_AniDB_TvDBV2> CrossRef_AniDB_TvDB { get; set; }
		public List<Contract_TvDB_Series> TvDBSeries { get; set; }
		public List<Contract_TvDB_Episode> TvDBEpisodes { get; set; }
		public List<Contract_TvDB_ImageFanart> TvDBImageFanarts { get; set; }
		public List<Contract_TvDB_ImagePoster> TvDBImagePosters { get; set; }
		public List<Contract_TvDB_ImageWideBanner> TvDBImageWideBanners { get; set; }

		// Trakt
		public Contract_CrossRef_AniDB_Trakt CrossRef_AniDB_Trakt { get; set; }
		public Contract_Trakt_Show TraktShow { get; set; }
		public Contract_Trakt_ImageFanart TraktImageFanart { get; set; }
		public Contract_Trakt_ImagePoster TraktImagePoster { get; set; }

		// MovieDB
		public Contract_CrossRef_AniDB_Other CrossRef_AniDB_MovieDB { get; set; }
		public Contract_MovieDB_Movie MovieDBMovie { get; set; }
		public List<Contract_MovieDB_Fanart> MovieDBFanarts { get; set; }
		public List<Contract_MovieDB_Poster> MovieDBPosters { get; set; }

		// MAL
		public List<Contract_CrossRef_AniDB_MAL> CrossRef_AniDB_MAL { get; set; }

		public Contract_AniDB_AnimeCrossRefs()
		{
			CrossRef_AniDB_TvDB = new List<Contract_CrossRef_AniDB_TvDBV2>();
			TvDBSeries = new List<Contract_TvDB_Series>();
			TvDBEpisodes = new List<Contract_TvDB_Episode>();
			TvDBImageFanarts = new List<Contract_TvDB_ImageFanart>();
			TvDBImagePosters = new List<Contract_TvDB_ImagePoster>();
			TvDBImageWideBanners = new List<Contract_TvDB_ImageWideBanner>();

			CrossRef_AniDB_MovieDB = null;
			MovieDBMovie = null;
			MovieDBFanarts = new List<Contract_MovieDB_Fanart>();
			MovieDBPosters = new List<Contract_MovieDB_Poster>();

			CrossRef_AniDB_MAL = null;
		}
	}
}
