using JMMContracts;
using JMMServer.Repositories;
using NHibernate;

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
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return ToContract(session);
            }
        }

        public Contract_AniDB_Anime_DefaultImage ToContract(ISession session)
        {
            var contract = new Contract_AniDB_Anime_DefaultImage();

            contract.AniDB_Anime_DefaultImageID = AniDB_Anime_DefaultImageID;
            contract.AnimeID = AnimeID;
            contract.ImageParentID = ImageParentID;
            contract.ImageParentType = ImageParentType;
            contract.ImageType = ImageType;

            contract.MovieFanart = null;
            contract.MoviePoster = null;
            contract.TVPoster = null;
            contract.TVFanart = null;
            contract.TVWideBanner = null;
            contract.TraktFanart = null;
            contract.TraktPoster = null;

            var imgType = (JMMImageType)ImageParentType;

            switch (imgType)
            {
                case JMMImageType.TvDB_Banner:

                    var repBanners = new TvDB_ImageWideBannerRepository();
                    var banner = repBanners.GetByID(session, ImageParentID);
                    if (banner != null) contract.TVWideBanner = banner.ToContract();

                    break;

                case JMMImageType.TvDB_Cover:

                    var repPosters = new TvDB_ImagePosterRepository();
                    var poster = repPosters.GetByID(session, ImageParentID);
                    if (poster != null) contract.TVPoster = poster.ToContract();

                    break;

                case JMMImageType.TvDB_FanArt:

                    var repFanart = new TvDB_ImageFanartRepository();
                    var fanart = repFanart.GetByID(session, ImageParentID);
                    if (fanart != null) contract.TVFanart = fanart.ToContract();

                    break;

                case JMMImageType.MovieDB_Poster:

                    var repMoviePosters = new MovieDB_PosterRepository();
                    var moviePoster = repMoviePosters.GetByID(session, ImageParentID);
                    if (moviePoster != null) contract.MoviePoster = moviePoster.ToContract();

                    break;

                case JMMImageType.MovieDB_FanArt:

                    var repMovieFanart = new MovieDB_FanartRepository();
                    var movieFanart = repMovieFanart.GetByID(session, ImageParentID);
                    if (movieFanart != null) contract.MovieFanart = movieFanart.ToContract();

                    break;

                case JMMImageType.Trakt_Fanart:

                    var repTraktFanart = new Trakt_ImageFanartRepository();
                    var traktFanart = repTraktFanart.GetByID(session, ImageParentID);
                    if (traktFanart != null) contract.TraktFanart = traktFanart.ToContract();

                    break;

                case JMMImageType.Trakt_Poster:

                    var repTraktPoster = new Trakt_ImagePosterRepository();
                    var traktPoster = repTraktPoster.GetByID(session, ImageParentID);
                    if (traktPoster != null) contract.TraktPoster = traktPoster.ToContract();

                    break;
            }

            return contract;
        }
    }
}