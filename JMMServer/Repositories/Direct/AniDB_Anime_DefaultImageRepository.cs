using System.Collections.Generic;
using JMMServer.Databases;
using JMMServer.Entities;
using Shoko.Models.Server;
using JMMServer.Repositories.NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class AniDB_Anime_DefaultImageRepository : BaseDirectRepository<SVR_AniDB_Anime_DefaultImage, int>
    {
        private AniDB_Anime_DefaultImageRepository()
        {
            
        }

        public static AniDB_Anime_DefaultImageRepository Create()
        {
            return new AniDB_Anime_DefaultImageRepository();
        }
        public SVR_AniDB_Anime_DefaultImage GetByAnimeIDAndImagezSizeType(int animeid, int imageType)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByAnimeIDAndImagezSizeType(session.Wrap(), animeid, imageType);
            }
        }

        public SVR_AniDB_Anime_DefaultImage GetByAnimeIDAndImagezSizeType(ISessionWrapper session, int animeid, int imageType)
        {
            SVR_AniDB_Anime_DefaultImage cr = session
                .CreateCriteria(typeof(SVR_AniDB_Anime_DefaultImage))
                .Add(Restrictions.Eq("AnimeID", animeid))
                .Add(Restrictions.Eq("ImageType", imageType))
                .UniqueResult<SVR_AniDB_Anime_DefaultImage>();
            return cr;
        }

        public List<SVR_AniDB_Anime_DefaultImage> GetByAnimeID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var cats = session
                    .CreateCriteria(typeof(SVR_AniDB_Anime_DefaultImage))
                    .Add(Restrictions.Eq("AnimeID", id))
                    .List<SVR_AniDB_Anime_DefaultImage>();

                return new List<SVR_AniDB_Anime_DefaultImage>(cats);
            }
        }

    }
}