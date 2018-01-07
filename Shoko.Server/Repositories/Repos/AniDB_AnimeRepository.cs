using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Utils;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Repos
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

        public override void PreInit(IProgress<InitProgress> progress, int batchSize)
        {

            List<SVR_AniDB_Anime> animeToUpdate =
                Where(a => a.ContractVersion < SVR_AniDB_Anime.CONTRACT_VERSION).ToList();
            if (animeToUpdate.Count == 0)
                return;
            InitProgress prog = new InitProgress();
            prog.Title = "Regenerating AniDB_Anime Contracts";
            prog.Step = 0;
            prog.Total = animeToUpdate.Count;
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
            prog.Step = animeToUpdate.Count;
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
        public List<string> GetAllReleaseGroups()
        {
            return Repo.AniDB_File.WhereAll().GroupBy(a => a.Anime_GroupName)
                .OrderByDescending(a => a.Select(b => b.AnimeID).Count()).ThenBy(b => b.Key).Select(a => a.Key)
                .ToList();
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

        public Dictionary<int, DefaultAnimeImages> GetDefaultImagesByAnime(IEnumerable<int> animeIds)
        {

            if (animeIds == null)
                throw new ArgumentNullException(nameof(animeIds));

            var defImagesByAnime = new Dictionary<int, DefaultAnimeImages>();

            Dictionary<int, List<AniDB_Anime_DefaultImage>> defs=Repo.AniDB_Anime_DefaultImage.GetByAnimeIDs(animeIds);
            Dictionary<ImageEntityType, List<AniDB_Anime_DefaultImage>> allimages = defs.Values.SelectMany(a => a).GroupBy(a=>(ImageEntityType)a.ImageParentType).ToDictionary(a=>a.Key,a=>a.ToList());
            Dictionary<ImageEntityType, Dictionary<int, IImageEntity>> allreferences=new Dictionary<ImageEntityType, Dictionary<int, IImageEntity>>();
            allreferences.Add(ImageEntityType.TvDB_Banner, Enumerable.ToDictionary<TvDB_ImageWideBanner, int, IImageEntity>(Repo.TvDB_ImageWideBanner.GetMany(allimages.SafeGetList(ImageEntityType.TvDB_Banner).Select(a=>a.ImageParentID)), a=>a.TvDB_ImageWideBannerID,a=> (IImageEntity)a));
            allreferences.Add(ImageEntityType.TvDB_Cover, Enumerable.ToDictionary<TvDB_ImagePoster, int, IImageEntity>(Repo.TvDB_ImagePoster.GetMany(allimages.SafeGetList(ImageEntityType.TvDB_Cover).Select(a => a.ImageParentID)), a=>a.TvDB_ImagePosterID,a=> (IImageEntity)a));
            allreferences.Add(ImageEntityType.TvDB_FanArt, Enumerable.ToDictionary<TvDB_ImageFanart, int, IImageEntity>(Repo.TvDB_ImageFanart.GetMany(allimages.SafeGetList(ImageEntityType.TvDB_FanArt).Select(a => a.ImageParentID)), a => a.TvDB_ImageFanartID, a => (IImageEntity)a));
            allreferences.Add(ImageEntityType.MovieDB_Poster, Enumerable.ToDictionary<MovieDB_Poster, int, IImageEntity>(Repo.MovieDB_Poster.GetMany(allimages.SafeGetList(ImageEntityType.MovieDB_Poster).Select(a => a.ImageParentID)), a=>a.MovieDB_PosterID,a=> (IImageEntity)a));
            allreferences.Add(ImageEntityType.MovieDB_FanArt, Enumerable.ToDictionary<MovieDB_Fanart, int, IImageEntity>(Repo.MovieDB_Fanart.GetMany(allimages.SafeGetList(ImageEntityType.MovieDB_FanArt).Select(a => a.ImageParentID)), a=>a.MovieDB_FanartID,a=> (IImageEntity)a));

            foreach (int aid in defs.Keys)
            {
                foreach (AniDB_Anime_DefaultImage imag in defs[aid])
                {
                    IImageEntity parentImage = allreferences[(ImageEntityType)imag.ImageParentType].FirstOrDefault(a => a.Key == imag.ImageParentID).Value; 
                    if (parentImage != null)
                    {
                        DefaultAnimeImage defImage = new DefaultAnimeImage(imag, parentImage);

                        if (!defImagesByAnime.TryGetValue(imag.AnimeID, out DefaultAnimeImages defImages))
                        {
                            defImages = new DefaultAnimeImages {AnimeID = imag.AnimeID};
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

        public AniDB_Anime_DefaultImage AniDBImage { get; }

        public ImageEntityType ParentImageType => (ImageEntityType) AniDBImage.ImageParentType;
    }
}

