using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shoko.Models.Server;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.RepositoriesV2.Repos
{
    public class AniDB_AnimeRepository : BaseRepository<SVR_AniDB_Anime, int>
    {
        private static PocoIndex<int, SVR_AniDB_Anime, int> Animes;
        internal override int SelectKey(SVR_AniDB_Anime entity) => entity.AnimeID;

        internal override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, SVR_AniDB_Anime, int>(Cache, a => a.AnimeID);
        }

        internal override void ClearIndexes()
        {
            Animes = null;
        }

        internal override object BeginSave(SVR_AniDB_Anime entity, SVR_AniDB_Anime original_entity, object parameters)
        {
            entity.UpdateContractDetailed();
            return null;
        }

        public override void Init(IProgress<RegenerateProgress> progress, int batchSize)
        {

            List<SVR_AniDB_Anime> animeToUpdate =
                Where(a => a.ContractVersion < SVR_AniDB_Anime.CONTRACT_VERSION).ToList();
            int max = animeToUpdate.Count;
            if (max == 0)
                return;
            int count = 0;
            RegenerateProgress prog = new RegenerateProgress();
            prog.Title = "Regenerating AniDB_Anime Contracts";
            prog.Step = 0;
            prog.Total = max;
            progress.Report(prog);


            BatchAction(animeToUpdate, batchSize, (anime, original) =>
            {
                anime.Description = anime.Description?.Replace("`", "\'") ?? string.Empty;
                anime.MainTitle = anime.MainTitle.Replace("`", "\'");
                anime.AllTags = anime.AllTags.Replace("`", "\'");
                anime.AllTitles = anime.AllTitles.Replace("`", "\'");
                prog.Step++;
                progress.Report(prog);
            });
            prog.Step = max;
            progress.Report(prog);
        }

        public SVR_AniDB_Anime GetByAnimeID(int id)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetOne(id);
                return Table.FirstOrDefault(a => a.AnimeID == id);
            }
        }

        public List<SVR_AniDB_Anime> GetForDate(DateTime startDate, DateTime endDate)
        {
            return Where(a => a.AirDate.HasValue && a.AirDate.Value >= startDate && a.AirDate.Value <= endDate)
                .ToList();
        }

        public List<SVR_AniDB_Anime> SearchByName(string queryText)
        {
            return Where(a => a.AllTitles.Contains(queryText, StringComparison.InvariantCultureIgnoreCase)).ToList();
        }

        public List<SVR_AniDB_Anime> SearchByTag(string queryText)
        {
            return Where(a => a.AllTags.Contains(queryText, StringComparison.InvariantCultureIgnoreCase)).ToList();
        }

        //TODO DBREFACTOR  
        public Dictionary<int, DefaultAnimeImages> GetDefaultImagesByAnime(int[] animeIds)
        {

            if (animeIds == null)
                throw new ArgumentNullException("animeIds");

            var defImagesByAnime = new Dictionary<int, DefaultAnimeImages>();

            if (animeIds.Length == 0) return defImagesByAnime;


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

