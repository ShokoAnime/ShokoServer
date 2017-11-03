using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using NHibernate;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories
{
    public class AniDB_AnimeRepository : BaseCachedRepository<SVR_AniDB_Anime, int>
    {
        private static PocoIndex<int, SVR_AniDB_Anime, int> Animes;

        private AniDB_AnimeRepository()
        {
        }

        public static AniDB_AnimeRepository Create()
        {
            return new AniDB_AnimeRepository();
        }

        protected override int SelectKey(SVR_AniDB_Anime entity)
        {
            return entity.AniDB_AnimeID;
        }

        public override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, SVR_AniDB_Anime, int>(Cache, a => a.AnimeID);
        }

        public override void RegenerateDb()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenStatelessSession())
            {
                const int batchSize = 50;
                ISessionWrapper sessionWrapper = session.Wrap();
                IList<SVR_AniDB_Anime> animeToUpdate = session.CreateCriteria<SVR_AniDB_Anime>()
                    .Add(Restrictions.Lt(nameof(SVR_AniDB_Anime.ContractVersion), SVR_AniDB_Anime.CONTRACT_VERSION))
                    .List<SVR_AniDB_Anime>();
                int max = animeToUpdate.Count;
                int count = 0;

                ServerState.Instance.CurrentSetupStatus = string.Format(
                    Commons.Properties.Resources.Database_Validating, typeof(AniDB_Anime).Name, " DbRegen");
                if (max <= 0) return;
                foreach (SVR_AniDB_Anime[] animeBatch in animeToUpdate.Batch(batchSize))
                {
                    SVR_AniDB_Anime.UpdateContractDetailedBatch(sessionWrapper, animeBatch);

                    using (ITransaction trans = session.BeginTransaction())
                    {
                        foreach (SVR_AniDB_Anime anime in animeBatch)
                        {
                            anime.Description = anime.Description?.Replace("`", "\'") ?? string.Empty;
                            anime.MainTitle = anime.MainTitle.Replace("`", "\'");
                            anime.AllTags = anime.AllTags.Replace("`", "\'");
                            anime.AllTitles = anime.AllTitles.Replace("`", "\'");
                            session.Update(anime);
                            Cache.Update(anime);
                            count++;
                        }

                        trans.Commit();
                    }

                    ServerState.Instance.CurrentSetupStatus = string.Format(
                        Commons.Properties.Resources.Database_Validating, typeof(AniDB_Anime).Name,
                        " DbRegen - " + count + "/" + max);
                }

                ServerState.Instance.CurrentSetupStatus = string.Format(
                    Commons.Properties.Resources.Database_Validating, typeof(AniDB_Anime).Name,
                    " DbRegen - " + max + "/" + max);
            }
        }

        public override void Save(SVR_AniDB_Anime obj)
        {
            lock (globalDBLock)
            {
                lock (obj)
                {
                    if (obj.AniDB_AnimeID == 0)
                    {
                        obj.Contract = null;
                        base.Save(obj);
                    }
                    using (var session = DatabaseFactory.SessionFactory.OpenSession())
                    {
                        obj.UpdateContractDetailed(session.Wrap());
                    }
                    // populate the database
                    base.Save(obj);
                }
            }
        }


        public SVR_AniDB_Anime GetByAnimeID(int id)
        {
            lock (Cache)
            {
                return Animes.GetOne(id);
            }
        }

        public SVR_AniDB_Anime GetByAnimeID(ISessionWrapper session, int id)
        {
            lock (Cache)
            {
                return Animes.GetOne(id);
            }
        }

        public List<SVR_AniDB_Anime> GetForDate(DateTime startDate, DateTime endDate)
        {
            lock (Cache)
            {
                return Cache.Values.Where(a =>
                    a.AirDate.HasValue && a.AirDate.Value >= startDate && a.AirDate.Value <= endDate).ToList();
            }
        }

        public List<SVR_AniDB_Anime> SearchByName(string queryText)
        {
            lock (Cache)
            {
                return Cache.Values.Where(a =>
                    a.AllTitles.IndexOf(queryText, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList();
            }
        }

        public List<SVR_AniDB_Anime> SearchByTag(string queryText)
        {
            lock (Cache)
            {
                return Cache.Values
                    .Where(a => a.AllTags.IndexOf(queryText, StringComparison.InvariantCultureIgnoreCase) >= 0)
                    .ToList();
            }
        }

        public Dictionary<int, DefaultAnimeImages> GetDefaultImagesByAnime(ISessionWrapper session, int[] animeIds)
        {
            if (session == null)
                throw new ArgumentNullException("session");
            if (animeIds == null)
                throw new ArgumentNullException("animeIds");

            var defImagesByAnime = new Dictionary<int, DefaultAnimeImages>();

            if (animeIds.Length == 0) return defImagesByAnime;

            lock (globalDBLock)
            {
                // TODO: Determine if joining on the correct columns
                var results = session.CreateSQLQuery(@"
                SELECT {defImg.*}, {tvWide.*}, {tvPoster.*}, {tvFanart.*}, {movPoster.*}, {movFanart.*}
                    FROM AniDB_Anime_DefaultImage defImg
                        LEFT OUTER JOIN TvDB_ImageWideBanner AS tvWide
                            ON tvWide.TvDB_ImageWideBannerID = defImg.ImageParentID AND defImg.ImageParentType = :tvdbBannerType
                        LEFT OUTER JOIN TvDB_ImagePoster AS tvPoster
                            ON tvPoster.TvDB_ImagePosterID = defImg.ImageParentID AND defImg.ImageParentType = :tvdbCoverType
                        LEFT OUTER JOIN TvDB_ImageFanart AS tvFanart
                            ON tvFanart.TvDB_ImageFanartID = defImg.ImageParentID AND defImg.ImageParentType = :tvdbFanartType
                        LEFT OUTER JOIN MovieDB_Poster AS movPoster
                            ON movPoster.MovieDB_PosterID = defImg.ImageParentID AND defImg.ImageParentType = :movdbPosterType
                        LEFT OUTER JOIN MovieDB_Fanart AS movFanart
                            ON movFanart.MovieDB_FanartID = defImg.ImageParentID AND defImg.ImageParentType = :movdbFanartType
                    WHERE defImg.AnimeID IN (:animeIds) AND defImg.ImageParentType IN (:tvdbBannerType, :tvdbCoverType, :tvdbFanartType, :movdbPosterType, :movdbFanartType)")
                    .AddEntity("defImg", typeof(AniDB_Anime_DefaultImage))
                    .AddEntity("tvWide", typeof(TvDB_ImageWideBanner))
                    .AddEntity("tvPoster", typeof(TvDB_ImagePoster))
                    .AddEntity("tvFanart", typeof(TvDB_ImageFanart))
                    .AddEntity("movPoster", typeof(MovieDB_Poster))
                    .AddEntity("movFanart", typeof(MovieDB_Fanart))
                    .SetParameterList("animeIds", animeIds)
                    .SetInt32("tvdbBannerType", (int) ImageEntityType.TvDB_Banner)
                    .SetInt32("tvdbCoverType", (int) ImageEntityType.TvDB_Cover)
                    .SetInt32("tvdbFanartType", (int) ImageEntityType.TvDB_FanArt)
                    .SetInt32("movdbPosterType", (int) ImageEntityType.MovieDB_Poster)
                    .SetInt32("movdbFanartType", (int) ImageEntityType.MovieDB_FanArt)
                    .List<object[]>();

                foreach (object[] result in results)
                {
                    var aniDbDefImage = (AniDB_Anime_DefaultImage) result[0];
                    IImageEntity parentImage = null;

                    switch ((ImageEntityType) aniDbDefImage.ImageParentType)
                    {
                        case ImageEntityType.TvDB_Banner:
                            parentImage = (IImageEntity) result[1];
                            break;
                        case ImageEntityType.TvDB_Cover:
                            parentImage = (IImageEntity) result[2];
                            break;
                        case ImageEntityType.TvDB_FanArt:
                            parentImage = (IImageEntity) result[3];
                            break;
                        case ImageEntityType.MovieDB_Poster:
                            parentImage = (IImageEntity) result[4];
                            break;
                        case ImageEntityType.MovieDB_FanArt:
                            parentImage = (IImageEntity) result[5];
                            break;
                    }

                    if (parentImage == null)
                    {
                        continue;
                    }

                    DefaultAnimeImage defImage = new DefaultAnimeImage(aniDbDefImage, parentImage);

                    if (!defImagesByAnime.TryGetValue(aniDbDefImage.AnimeID, out DefaultAnimeImages defImages))
                    {
                        defImages = new DefaultAnimeImages {AnimeID = aniDbDefImage.AnimeID};
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
            }

            return defImagesByAnime;
        }
    }

    public class DefaultAnimeImages
    {
        public CL_AniDB_Anime_DefaultImage GetPosterContractNoBlanks()
        {
            if (Poster != null)
            {
                return Poster.ToContract();
            }

            return new CL_AniDB_Anime_DefaultImage
            {
                AnimeID = AnimeID,
                ImageType = (int) ImageEntityType.AniDB_Cover
            };
        }

        public CL_AniDB_Anime_DefaultImage GetFanartContractNoBlanks(CL_AniDB_Anime anime)
        {
            if (anime == null)
                throw new ArgumentNullException(nameof(anime));

            if (Fanart != null)
            {
                return Fanart.ToContract();
            }

            List<CL_AniDB_Anime_DefaultImage> fanarts = anime.Fanarts;

            if (fanarts == null || fanarts.Count == 0)
            {
                return null;
            }
            if (fanarts.Count == 1)
            {
                return fanarts[0];
            }

            Random random = new Random();

            return fanarts[random.Next(0, fanarts.Count - 1)];
        }

        public int AnimeID { get; set; }

        public DefaultAnimeImage Poster { get; set; }

        public DefaultAnimeImage Fanart { get; set; }

        public DefaultAnimeImage WideBanner { get; set; }
    }

    public class DefaultAnimeImage
    {
        private readonly IImageEntity _parentImage;

        public DefaultAnimeImage(AniDB_Anime_DefaultImage aniDbImage, IImageEntity parentImage)
        {
            AniDBImage = aniDbImage ?? throw new ArgumentNullException(nameof(aniDbImage));
            _parentImage = parentImage ?? throw new ArgumentNullException(nameof(parentImage));
        }

        public CL_AniDB_Anime_DefaultImage ToContract()
        {
            return AniDBImage.ToClient(_parentImage);
        }

        public TImageType GetParentImage<TImageType>()
            where TImageType : class, IImageEntity => _parentImage as TImageType;

        public ImageSizeType AniDBImageSizeType => (ImageSizeType) AniDBImage.ImageType;

        public AniDB_Anime_DefaultImage AniDBImage { get; private set; }

        public ImageEntityType ParentImageType => (ImageEntityType) AniDBImage.ImageParentType;
    }
}