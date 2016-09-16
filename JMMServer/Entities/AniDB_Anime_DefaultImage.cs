using JMMContracts;
using JMMServer.Repositories;
using JMMServer.Repositories.Direct;
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
                    parentImage = RepoFactory.TvDB_ImageWideBanner.GetByID(session, ImageParentID);
                    break;
                case JMMImageType.TvDB_Cover:
                    parentImage = RepoFactory.TvDB_ImagePoster.GetByID(session, ImageParentID);
                    break;
                case JMMImageType.TvDB_FanArt:
                    parentImage = RepoFactory.TvDB_ImageFanart.GetByID(session, ImageParentID);
                    break;
                case JMMImageType.MovieDB_Poster:
                    parentImage = RepoFactory.MovieDB_Poster.GetByID(session, ImageParentID);
                    break;
                case JMMImageType.MovieDB_FanArt:
                    parentImage = RepoFactory.MovieDB_Fanart.GetByID(session, ImageParentID);
                    break;
                case JMMImageType.Trakt_Fanart:
                    parentImage = RepoFactory.Trakt_ImageFanart.GetByID(session, ImageParentID);
                    break;
                case JMMImageType.Trakt_Poster:
                    parentImage = RepoFactory.Trakt_ImagePoster.GetByID(session, ImageParentID);
                    break;
            }

            return ToContract(parentImage);
        }
    }
}