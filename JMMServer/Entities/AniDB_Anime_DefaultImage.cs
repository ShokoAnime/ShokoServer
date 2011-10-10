using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;
using JMMServer.Repositories;

namespace JMMServer.Entities
{
	public class AniDB_Anime_DefaultImage
	{
		public int AniDB_Anime_DefaultImageID { get; private set; }
		public int AnimeID { get; set; }
		public int ImageParentID { get; set; }
		public int ImageParentType { get; set; }
		public int ImageType { get; set; }

		public Contract_AniDB_Anime_DefaultImage ToContract()
		{
			Contract_AniDB_Anime_DefaultImage contract = new Contract_AniDB_Anime_DefaultImage();

			contract.AniDB_Anime_DefaultImageID = this.AniDB_Anime_DefaultImageID;
			contract.AnimeID = this.AnimeID;
			contract.ImageParentID = this.ImageParentID;
			contract.ImageParentType = this.ImageParentType;
			contract.ImageType = this.ImageType;

			contract.MovieFanart = null;
			contract.MoviePoster = null;
			contract.TVPoster = null;
			contract.TVFanart = null;
			contract.TVWideBanner = null;
			contract.TraktFanart = null;
			contract.TraktPoster = null;

			JMMImageType imgType = (JMMImageType)ImageParentType;

			switch (imgType)
			{
				case JMMImageType.TvDB_Banner:

					TvDB_ImageWideBannerRepository repBanners = new TvDB_ImageWideBannerRepository();
					TvDB_ImageWideBanner banner = repBanners.GetByID(ImageParentID);
					if (banner != null) contract.TVWideBanner = banner.ToContract();

					break;

				case JMMImageType.TvDB_Cover:

					TvDB_ImagePosterRepository repPosters = new TvDB_ImagePosterRepository();
					TvDB_ImagePoster poster = repPosters.GetByID(ImageParentID);
					if (poster != null) contract.TVPoster = poster.ToContract();

					break;

				case JMMImageType.TvDB_FanArt:

					TvDB_ImageFanartRepository repFanart = new TvDB_ImageFanartRepository();
					TvDB_ImageFanart fanart = repFanart.GetByID(ImageParentID);
					if (fanart != null) contract.TVFanart = fanart.ToContract();

					break;

				case JMMImageType.MovieDB_Poster:

					MovieDB_PosterRepository repMoviePosters = new MovieDB_PosterRepository();
					MovieDB_Poster moviePoster = repMoviePosters.GetByID(ImageParentID);
					if (moviePoster != null) contract.MoviePoster = moviePoster.ToContract();

					break;

				case JMMImageType.MovieDB_FanArt:

					MovieDB_FanartRepository repMovieFanart = new MovieDB_FanartRepository();
					MovieDB_Fanart movieFanart = repMovieFanart.GetByID(ImageParentID);
					if (movieFanart != null) contract.MovieFanart = movieFanart.ToContract();

					break;

				case JMMImageType.Trakt_Fanart:

					Trakt_ImageFanartRepository repTraktFanart = new Trakt_ImageFanartRepository();
					Trakt_ImageFanart traktFanart = repTraktFanart.GetByID(ImageParentID);
					if (traktFanart != null) contract.TraktFanart = traktFanart.ToContract();

					break;

				case JMMImageType.Trakt_Poster:

					Trakt_ImagePosterRepository repTraktPoster = new Trakt_ImagePosterRepository();
					Trakt_ImagePoster traktPoster = repTraktPoster.GetByID(ImageParentID);
					if (traktPoster != null) contract.TraktPoster = traktPoster.ToContract();

					break;
			}

			return contract;
		}
	}
}
