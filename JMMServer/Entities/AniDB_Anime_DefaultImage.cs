using JMMContracts;
using JMMServer.Repositories;
using JMMServer.Repositories.NHibernate;
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
                return ToContract(session.Wrap());
            }
        }

        public Contract_AniDB_Anime_DefaultImage ToContract(IImageEntity parentImage)
        {
            var contract = new Contract_AniDB_Anime_DefaultImage
                {
                    AniDB_Anime_DefaultImageID = AniDB_Anime_DefaultImageID,
                    AnimeID = AnimeID,
                    ImageParentID = ImageParentID,
                    ImageParentType = ImageParentType,
                    ImageType = ImageType
                };

            JMMImageType imgType = (JMMImageType)ImageParentType;

            switch (imgType)
            {
                case JMMImageType.TvDB_Banner:
                    contract.TVWideBanner = (parentImage as TvDB_ImageWideBanner)?.ToContract();
                    break;
                case JMMImageType.TvDB_Cover:
                    contract.TVPoster = (parentImage as TvDB_ImagePoster)?.ToContract();
                    break;
                case JMMImageType.TvDB_FanArt:
                    contract.TVFanart = (parentImage as TvDB_ImageFanart)?.ToContract();
                    break;
                case JMMImageType.MovieDB_Poster:
                    contract.MoviePoster = (parentImage as MovieDB_Poster)?.ToContract();
                    break;
                case JMMImageType.MovieDB_FanArt:
                    contract.MovieFanart = (parentImage as MovieDB_Fanart)?.ToContract();
                    break;
                case JMMImageType.Trakt_Fanart:
                    contract.TraktFanart = (parentImage as Trakt_ImageFanart)?.ToContract();
                    break;
                case JMMImageType.Trakt_Poster:
                    contract.TraktPoster = (parentImage as Trakt_ImagePoster)?.ToContract();
                    break;
            }

            return contract;
        }

        public Contract_AniDB_Anime_DefaultImage ToContract(ISessionWrapper session)
        {
            JMMImageType imgType = (JMMImageType)ImageParentType;
            IImageEntity parentImage = null;

            switch (imgType)
            {
                case JMMImageType.TvDB_Banner:
                    TvDB_ImageWideBannerRepository repBanners = new TvDB_ImageWideBannerRepository();

                    parentImage = repBanners.GetByID(session, ImageParentID);
                    break;
                case JMMImageType.TvDB_Cover:
                    TvDB_ImagePosterRepository repPosters = new TvDB_ImagePosterRepository();

                    parentImage = repPosters.GetByID(session, ImageParentID);
                    break;
                case JMMImageType.TvDB_FanArt:
                    TvDB_ImageFanartRepository repFanart = new TvDB_ImageFanartRepository();

                    parentImage = repFanart.GetByID(session, ImageParentID);
                    break;
                case JMMImageType.MovieDB_Poster:
                    MovieDB_PosterRepository repMoviePosters = new MovieDB_PosterRepository();

                    parentImage = repMoviePosters.GetByID(session, ImageParentID);
                    break;
                case JMMImageType.MovieDB_FanArt:
                    MovieDB_FanartRepository repMovieFanart = new MovieDB_FanartRepository();

                    parentImage = repMovieFanart.GetByID(session, ImageParentID);
                    break;
                case JMMImageType.Trakt_Fanart:
                    Trakt_ImageFanartRepository repTraktFanart = new Trakt_ImageFanartRepository();
                    parentImage = repTraktFanart.GetByID(session, ImageParentID);
                    break;
                case JMMImageType.Trakt_Poster:
                    Trakt_ImagePosterRepository repTraktPoster = new Trakt_ImagePosterRepository();

                    parentImage = repTraktPoster.GetByID(session, ImageParentID);
                    break;
            }

            return ToContract(parentImage);
        }
    }
}