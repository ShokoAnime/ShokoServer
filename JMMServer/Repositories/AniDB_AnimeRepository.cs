using System;
using System.Collections.Generic;
using JMMContracts;
using JMMServer.Collections;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class AniDB_AnimeRepository
    {
        public void Save(AniDB_Anime obj)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                Save(session, obj);
            }
        }

        public void Save(ISession session, AniDB_Anime obj)
        {
            lock (obj)
            {
                if (obj.AniDB_AnimeID == 0)
                {
                    obj.Contract = null;
                    using (var transaction = session.BeginTransaction())
                    {
                        session.SaveOrUpdate(obj);
                        transaction.Commit();
                    }
                }

                obj.UpdateContractDetailed(session.Wrap());
                // populate the database

                using (var transaction = session.BeginTransaction())
                {
                    session.SaveOrUpdate(obj);
                    transaction.Commit();
                }
            }
        }

        public static void InitCache()
        {
            string t = "AniDB_Anime";
            ServerState.Instance.CurrentSetupStatus = string.Format(Properties.Resources.Database_Cache, t, string.Empty);

            using (var session = JMMService.SessionFactory.OpenStatelessSession())
            {
                const int batchSize = 50;
                ISessionWrapper sessionWrapper = session.Wrap();
                IList<AniDB_Anime> animeToUpdate = session.CreateCriteria<AniDB_Anime>()
                    .Add(Restrictions.Lt(nameof(AniDB_Anime.ContractVersion), AniDB_Anime.CONTRACT_VERSION))
                    .List<AniDB_Anime>();
                int max = animeToUpdate.Count;
                int count = 0;

                foreach (AniDB_Anime[] animeBatch in animeToUpdate.Batch(batchSize))
                {
                    AniDB_Anime.UpdateContractDetailedBatch(sessionWrapper, animeBatch);

                    using (ITransaction trans = session.BeginTransaction())
                    {
                        foreach (AniDB_Anime anime in animeBatch)
                        {
                            session.Update(anime);
                            count++;
                        }

                        trans.Commit();
                    }

                    ServerState.Instance.CurrentSetupStatus = string.Format(Properties.Resources.Database_Cache, t, " DbRegen - " + count + "/" + max);
                }

                ServerState.Instance.CurrentSetupStatus = string.Format(Properties.Resources.Database_Cache, t, " DbRegen - " + max + "/" + max);
            }
        }

        public AniDB_Anime GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<AniDB_Anime>(id);
            }
        }

        public AniDB_Anime GetByAnimeID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                AniDB_Anime cr = session
                    .CreateCriteria(typeof(AniDB_Anime))
                    .Add(Restrictions.Eq("AnimeID", id))
                    .UniqueResult<AniDB_Anime>();
                return cr;
            }
        }

        public AniDB_Anime GetByAnimeID(ISessionWrapper session, int id)
        {
            AniDB_Anime cr = session
                .CreateCriteria(typeof(AniDB_Anime))
                .Add(Restrictions.Eq("AnimeID", id))
                .UniqueResult<AniDB_Anime>();
            return cr;
        }

        public List<AniDB_Anime> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetAll(session);
            }
        }

        public List<AniDB_Anime> GetAll(ISession session)
        {
            var objs = session
                .CreateCriteria(typeof(AniDB_Anime))
                .List<AniDB_Anime>();

            return new List<AniDB_Anime>(objs);
        }

        public List<AniDB_Anime> GetForDate(DateTime startDate, DateTime endDate)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetForDate(session, startDate, endDate);
            }
        }

        public List<AniDB_Anime> GetForDate(ISession session, DateTime startDate, DateTime endDate)
        {
            var objs = session
                .CreateCriteria(typeof(AniDB_Anime))
                .Add(Restrictions.Ge("AirDate", startDate))
                .Add(Restrictions.Le("AirDate", endDate))
                .AddOrder(Order.Desc("AirDate"))
                .List<AniDB_Anime>();

            return new List<AniDB_Anime>(objs);
        }

        public List<AniDB_Anime> SearchByName(ISession session, string queryText)
        {
            var objs = session
                .CreateCriteria(typeof(AniDB_Anime))
                .Add(Restrictions.InsensitiveLike("AllTitles", queryText, MatchMode.Anywhere))
                .List<AniDB_Anime>();

            return new List<AniDB_Anime>(objs);
        }

        public List<AniDB_Anime> SearchByName(string queryText)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_Anime))
                    .Add(Restrictions.InsensitiveLike("AllTitles", queryText, MatchMode.Anywhere))
                    .List<AniDB_Anime>();

                return new List<AniDB_Anime>(objs);
            }
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    AniDB_Anime cr = GetByID(id);
                    if (cr != null)
                    {
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }

        public List<AniDB_Anime> SearchByTag(string queryText)
        {
            
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_Anime))
                    .Add(Restrictions.InsensitiveLike("AllTags", queryText, MatchMode.Anywhere))
                    .List<AniDB_Anime>();

                return new List<AniDB_Anime>(objs);
            }
        }

        public Dictionary<int, DefaultAnimeImages> GetDefaultImagesByAnime(ISessionWrapper session, int[] animeIds)
        {
            if (session == null)
                throw new ArgumentNullException("session");
            if (animeIds == null)
                throw new ArgumentNullException("animeIds");

            var defImagesByAnime = new Dictionary<int, DefaultAnimeImages>();

            if (animeIds.Length == 0)
            {
                return defImagesByAnime;
            }

            // TODO: Determine if joining on the correct columns
            var results = session.CreateSQLQuery($@"
                SELECT {{defImg.*}}, {{tvWide.*}}, {{tvPoster.*}}, {{tvFanart.*}}, {{movPoster.*}}, {{movFanart.*}}, {{traktFanart.*}}, {{traktPoster.*}}
                    FROM AniDB_Anime_DefaultImage defImg
                        LEFT OUTER JOIN TvDB_ImageWideBanner AS tvWide
                            ON tvWide.TvDB_ImageWideBannerID = defImg.ImageParentID AND defImg.ImageParentType = {JMMImageType.TvDB_Banner:D}
                        LEFT OUTER JOIN TvDB_ImagePoster AS tvPoster
                            ON tvPoster.TvDB_ImagePosterID = defImg.ImageParentID AND defImg.ImageParentType = {JMMImageType.TvDB_Cover:D}
                        LEFT OUTER JOIN TvDB_ImageFanart AS tvFanart
                            ON tvFanart.TvDB_ImageFanartID = defImg.ImageParentID AND defImg.ImageParentType = {JMMImageType.TvDB_FanArt:D}
                        LEFT OUTER JOIN MovieDB_Poster AS movPoster
                            ON movPoster.MovieDB_PosterID = defImg.ImageParentID AND defImg.ImageParentType = {JMMImageType.MovieDB_Poster:D}
                        LEFT OUTER JOIN MovieDB_Fanart AS movFanart
                            ON movFanart.MovieDB_FanartID = defImg.ImageParentID AND defImg.ImageParentType = {JMMImageType.MovieDB_FanArt:D}
                        LEFT OUTER JOIN Trakt_ImageFanart AS traktFanart
                            ON traktFanart.Trakt_ImageFanartID = defImg.ImageParentID AND defImg.ImageParentType = {JMMImageType.Trakt_Fanart:D}
                        LEFT OUTER JOIN Trakt_ImagePoster AS traktPoster
                            ON traktPoster.Trakt_ImagePosterID = defImg.ImageParentID AND defImg.ImageParentType = {JMMImageType.Trakt_Poster:D}
                    WHERE defImg.AnimeID IN (:animeIds)")
                .AddEntity("defImg", typeof(AniDB_Anime_DefaultImage))
                .AddEntity("tvWide", typeof(TvDB_ImageWideBanner))
                .AddEntity("tvPoster", typeof(TvDB_ImagePoster))
                .AddEntity("tvFanart", typeof(TvDB_ImageFanart))
                .AddEntity("movPoster", typeof(MovieDB_Poster))
                .AddEntity("movFanart", typeof(MovieDB_Fanart))
                .AddEntity("traktFanart", typeof(Trakt_ImageFanart))
                .AddEntity("traktPoster", typeof(Trakt_ImagePoster))
                .SetParameterList("animeIds", animeIds)
                .List<object[]>();

            foreach (object[] result in results)
            {
                var aniDbDefImage = (AniDB_Anime_DefaultImage)result[0];
                IImageEntity parentImage = null;

                switch ((JMMImageType)aniDbDefImage.ImageParentType)
                {
                    case JMMImageType.TvDB_Banner:
                        parentImage = (IImageEntity)result[1];
                        break;
                    case JMMImageType.TvDB_Cover:
                        parentImage = (IImageEntity)result[2];
                        break;
                    case JMMImageType.TvDB_FanArt:
                        parentImage = (IImageEntity)result[3];
                        break;
                    case JMMImageType.MovieDB_Poster:
                        parentImage = (IImageEntity)result[4];
                        break;
                    case JMMImageType.MovieDB_FanArt:
                        parentImage = (IImageEntity)result[5];
                        break;
                    case JMMImageType.Trakt_Fanart:
                        parentImage = (IImageEntity)result[6];
                        break;
                    case JMMImageType.Trakt_Poster:
                        parentImage = (IImageEntity)result[7];
                        break;
                }

                DefaultAnimeImages defImages;
                DefaultAnimeImage defImage = new DefaultAnimeImage(aniDbDefImage, parentImage);

                if (!defImagesByAnime.TryGetValue(aniDbDefImage.AnimeID, out defImages))
                {
                    defImages = new DefaultAnimeImages { AnimeID = aniDbDefImage.AnimeID };
                    defImagesByAnime.Add(defImages.AnimeID, defImages);
                }

                switch (defImage.AniDBImageSizeType)
                {
                    case ImageSizeType.Poster:
                        defImages.Poster = defImage;
                        break;
                    case ImageSizeType.WideBanner:
                        defImages.WideBanner = defImage;
                        break;
                    case ImageSizeType.Fanart:
                        defImages.Fanart = defImage;
                        break;
                }
            }

            return defImagesByAnime;
        }
    }

    public class DefaultAnimeImages
    {
        public int AnimeID { get; set; }

        public DefaultAnimeImage Poster { get; set; }

        public DefaultAnimeImage Fanart { get; set; }

        public DefaultAnimeImage WideBanner { get; set;  }
    }

    public class DefaultAnimeImage
    {
        private readonly IImageEntity _parentImage;

        public DefaultAnimeImage(AniDB_Anime_DefaultImage aniDbImage, IImageEntity parentImage)
        {
            if (aniDbImage == null)
                throw new ArgumentNullException(nameof(aniDbImage));
            if (parentImage == null)
                throw new ArgumentNullException(nameof(parentImage));

            AniDBImage = aniDbImage;
            _parentImage = parentImage;
        }

        public Contract_AniDB_Anime_DefaultImage ToContract()
        {
            return AniDBImage.ToContract(_parentImage);
        }

        public TImageType GetParentImage<TImageType>() where TImageType : class, IImageEntity  => _parentImage as TImageType;

        public ImageSizeType AniDBImageSizeType => (ImageSizeType)AniDBImage.ImageType;

        public AniDB_Anime_DefaultImage AniDBImage { get; private set; }

        public JMMImageType ParentImageType => (JMMImageType)AniDBImage.ImageParentType;
    }
}