using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Interfaces;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Entities
{
    // ReSharper disable once InconsistentNaming
    public class SVR_AniDB_Anime_DefaultImage : AniDB_Anime_DefaultImage
    {
        public SVR_AniDB_Anime_DefaultImage() //Empty Constructor for nhibernate
        {

        }

        public static CL_AniDB_Anime_DefaultImage ToClient(SVR_AniDB_Anime_DefaultImage defaultimage)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return defaultimage.ToClient(session.Wrap());
            }
        }

        public static CL_AniDB_Anime_DefaultImage ToClient(SVR_AniDB_Anime_DefaultImage defaultimage, IImageEntity parentImage)
        {
           
            CL_AniDB_Anime_DefaultImage contract = defaultimage.CloneToClient();
            JMMImageType imgType = (JMMImageType) defaultimage.ImageParentType;

            switch (imgType)
            {
                case JMMImageType.TvDB_Banner:
                    contract.TVWideBanner = (parentImage as TvDB_ImageWideBanner);
                    break;
                case JMMImageType.TvDB_Cover:
                    contract.TVPoster = (parentImage as TvDB_ImagePoster);
                    break;
                case JMMImageType.TvDB_FanArt:
                    contract.TVFanart = (parentImage as TvDB_ImageFanart);
                    break;
                case JMMImageType.MovieDB_Poster:
                    contract.MoviePoster = (parentImage as MovieDB_Poster);
                    break;
                case JMMImageType.MovieDB_FanArt:
                    contract.MovieFanart = (parentImage as MovieDB_Fanart);
                    break;
                case JMMImageType.Trakt_Fanart:
                    contract.TraktFanart = (parentImage as Trakt_ImageFanart);
                    break;
                case JMMImageType.Trakt_Poster:
                    contract.TraktPoster = (parentImage as Trakt_ImagePoster);
                    break;
            }

            return contract;
        }

        public CL_AniDB_Anime_DefaultImage ToClient(ISessionWrapper session)
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

            return ToClient(this, parentImage);
        }
    }
}