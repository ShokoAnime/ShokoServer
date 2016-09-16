using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using JMMContracts;
using JMMServer.Collections;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
    public class AniDB_AnimeRepository : BaseCachedRepository<AniDB_Anime, int>
    {

        private static PocoIndex<int, AniDB_Anime, int> Animes;

        private AniDB_AnimeRepository()
        {
            
        }

        public static AniDB_AnimeRepository Create()
        {
            return new AniDB_AnimeRepository();
        }
        public override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, AniDB_Anime, int>(Cache, a => a.AnimeID);
        }

        public override void RegenerateDb()
        {
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

                    ServerState.Instance.CurrentSetupStatus = string.Format(Properties.Resources.Database_Cache, typeof(AniDB_Anime).Name, " DbRegen - " + count + "/" + max);
                }

                ServerState.Instance.CurrentSetupStatus = string.Format(Properties.Resources.Database_Cache, typeof(AniDB_Anime).Name, " DbRegen - " + max + "/" + max);
            }
        }



        public override void Save(List<AniDB_Anime> objs)
        {
            foreach(AniDB_Anime o in objs)
                Save(o);
        }

        public override void Save(AniDB_Anime obj)
        {
            lock (obj)
            {
                if (obj.AniDB_AnimeID == 0)
                {
                    obj.Contract = null;
                    base.Save(obj);
                }
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    obj.UpdateContractDetailed(session.Wrap());
                }
                // populate the database
                base.Save(obj);
            }
        }






        public AniDB_Anime GetByAnimeID(int id)
        {
            return Animes.GetOne(id);
        }

        public AniDB_Anime GetByAnimeID(ISessionWrapper session, int id)
        {
            return Animes.GetOne(id);
        }

        public List<AniDB_Anime> GetForDate(DateTime startDate, DateTime endDate)
        {
            return Cache.Values.Where(a=>a.AirDate.HasValue && a.AirDate.Value>=startDate && a.AirDate.Value<=endDate).ToList();
        }

        public List<AniDB_Anime> GetForDate(ISession session, DateTime startDate, DateTime endDate)
        {
            return Cache.Values.Where(a => a.AirDate.HasValue && a.AirDate.Value >= startDate && a.AirDate.Value <= endDate).ToList();
        }

        public List<AniDB_Anime> SearchByName(ISession session, string queryText)
        {
            return Cache.Values.Where(a=>a.AllTitles.IndexOf(queryText,StringComparison.InvariantCultureIgnoreCase)>=0).ToList();
        }

        public List<AniDB_Anime> SearchByName(string queryText)
        {
            return Cache.Values.Where(a => a.AllTitles.IndexOf(queryText, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList();
        }

        public List<AniDB_Anime> SearchByTag(string queryText)
        {
            return Cache.Values.Where(a => a.AllTags.IndexOf(queryText, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList();
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