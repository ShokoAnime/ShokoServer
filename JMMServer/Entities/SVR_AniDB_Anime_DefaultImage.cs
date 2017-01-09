using JMMServer.Databases;
using JMMServer.Repositories;
using JMMServer.Repositories.NHibernate;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Server;

namespace JMMServer.Entities
{
    // ReSharper disable once InconsistentNaming
    public class SVR_AniDB_Anime_DefaultImage : AniDB_Anime_DefaultImage
    {
        public CL_AniDB_Anime_DefaultImage ToClient()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return ToClient(session.Wrap());
            }
        }

        public CL_AniDB_Anime_DefaultImage ToClient(IImageEntity parentImage)
        {
           
            CL_AniDB_Anime_DefaultImage contract = this.CloneToClient();
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

            return ToClient(parentImage);
        }
    }
}