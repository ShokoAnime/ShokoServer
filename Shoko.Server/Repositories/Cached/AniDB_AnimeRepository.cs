using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Cached;

public class AniDB_AnimeRepository : BaseCachedRepository<SVR_AniDB_Anime, int>
{
    private static PocoIndex<int, SVR_AniDB_Anime, int> Animes;

    public AniDB_AnimeRepository(DatabaseFactory databaseFactory) : base(databaseFactory) { }

    protected override int SelectKey(SVR_AniDB_Anime entity)
    {
        return entity.AniDB_AnimeID;
    }

    public override void PopulateIndexes()
    {
        Animes = new PocoIndex<int, SVR_AniDB_Anime, int>(Cache, a => a.AnimeID);
    }

    public override void RegenerateDb() { }

    public override void Save(SVR_AniDB_Anime obj)
    {
        Save(obj, true);
    }

    public void Save(SVR_AniDB_Anime obj, bool generateTvDBMatches)
    {
        if (obj.AniDB_AnimeID == 0)
        {
            base.Save(obj);
        }

        // populate the database
        base.Save(obj);

        if (generateTvDBMatches)
        {
            // Update TvDB Linking. Doing it here as updating anime updates episode info in batch
            lock (obj) TvDBLinkingHelper.GenerateTvDBEpisodeMatches(obj.AnimeID);
        }
    }

    public SVR_AniDB_Anime GetByAnimeID(int id)
    {
        return ReadLock(() => Animes.GetOne(id));
    }

    public SVR_AniDB_Anime GetByAnimeID(ISessionWrapper session, int id)
    {
        return GetByAnimeID(id);
    }

    public List<SVR_AniDB_Anime> GetForDate(DateTime startDate, DateTime endDate)
    {
        return ReadLock(() =>
            Cache.Values.Where(a => a.AirDate.HasValue && a.AirDate.Value >= startDate && a.AirDate.Value <= endDate)
                .ToList());
    }

    public List<SVR_AniDB_Anime> SearchByName(string queryText)
    {
        return ReadLock(() =>
            Cache.Values.Where(a => a.AllTitles.Contains(queryText, StringComparison.InvariantCultureIgnoreCase))
                .ToList());
    }

    public Dictionary<int, DefaultAnimeImages> GetDefaultImagesByAnime(ISessionWrapper session, int[] animeIds)
    {
        ArgumentNullException.ThrowIfNull(session, nameof(session));
        ArgumentNullException.ThrowIfNull(animeIds, nameof(animeIds));
        var defImagesByAnime = new Dictionary<int, DefaultAnimeImages>();

        if (animeIds is { Length: 0 })
            return defImagesByAnime;

        // treating cache as a global DB lock, as well
        var results = Lock(() =>
        {
            // TODO: Determine if joining on the correct columns
            return session.CreateSQLQuery(
                    @"SELECT {prefImg.*}, {tvdbBanner.*}, {tvdbPoster.*}, {tvdbBackdrop.*}, {tmdbPoster.*}, {tmdbBackdrop.*}
                    FROM AniDB_Anime_PreferredImage prefImg
                        LEFT OUTER JOIN TvDB_ImageWideBanner AS tvdbBanner
                            ON tvdbBanner.TvDB_ImageWideBannerID = prefImg.ImageID AND prefImg.ImageSource = :tvdbSourceType AND prefImg.ImageType = :imageBannerType
                        LEFT OUTER JOIN TvDB_ImagePoster AS tvdbPoster
                            ON tvdbPoster.TvDB_ImagePosterID = prefImg.ImageID AND prefImg.ImageSource = :tvdbSourceType AND prefImg.ImageType = :imagePosterType
                        LEFT OUTER JOIN TvDB_ImageFanart AS tvdbBackdrop
                            ON tvdbBackdrop.TvDB_ImageFanartID = prefImg.ImageID AND prefImg.ImageSource = :tvdbSourceType AND prefImg.ImageType = :imageBackdropType
                        LEFT OUTER JOIN TMDB_Image AS tmdbPoster
                            ON tmdbPoster.ImageType = :imagePosterType AND tmdbPoster.TMDB_ImageID = prefImg.ImageID AND prefImg.ImageSource = :tmdbSourceType AND prefImg.ImageParentType = :imagePosterType
                        LEFT OUTER JOIN TMDB_Image AS tmdbBackdrop
                            ON tmdbBackdrop.ImageType = :imageBackdropType AND tmdbBackdrop.TMDB_ImageID = prefImg.ImageID AND prefImg.ImageSource = :tmdbSourceType AND prefImg.ImageType = :imageBackdropType
                    WHERE prefImg.AnimeID IN (:animeIds) AND prefImg.ImageType IN (:imageBannerType, :imagePosterType, :imageBackdropType)"
                )
                .AddEntity("prefImg", typeof(AniDB_Anime_PreferredImage))
                .AddEntity("tvdbBanner", typeof(TvDB_ImageWideBanner))
                .AddEntity("tvdbPoster", typeof(TvDB_ImagePoster))
                .AddEntity("tvdbBackdrop", typeof(TvDB_ImageFanart))
                .AddEntity("tmdbPoster", typeof(TMDB_Image))
                .AddEntity("tmdbBackdrop", typeof(TMDB_Image))
                .SetParameterList("animeIds", animeIds)
                .SetInt32("tvdbSourceType", (int)DataSourceType.TvDB)
                .SetInt32("tmdbSourceType", (int)DataSourceType.TMDB)
                .SetInt32("imageBackdropType", (int)ImageEntityType.Backdrop)
                .SetInt32("imageBannerType", (int)ImageEntityType.Banner)
                .SetInt32("imagePosterType", (int)ImageEntityType.Poster)
                .List<object[]>();
        });

        foreach (var result in results)
        {
            var preferredImage = (AniDB_Anime_PreferredImage)result[0];
            IImageEntity image = null;
            switch (preferredImage.ImageType.ToClient(preferredImage.ImageSource))
            {
                case CL_ImageEntityType.TvDB_Banner:
                    image = (IImageEntity)result[1];
                    break;
                case CL_ImageEntityType.TvDB_Cover:
                    image = (IImageEntity)result[2];
                    break;
                case CL_ImageEntityType.TvDB_FanArt:
                    image = (IImageEntity)result[3];
                    break;
                case CL_ImageEntityType.MovieDB_Poster:
                    image = ((TMDB_Image)result[4]).ToClientPoster();
                    break;
                case CL_ImageEntityType.MovieDB_FanArt:
                    image = ((TMDB_Image)result[5]).ToClientFanart();
                    break;
            }

            if (image == null)
                continue;

            if (!defImagesByAnime.TryGetValue(preferredImage.AnidbAnimeID, out var defImages))
            {
                defImages = new DefaultAnimeImages { AnimeID = preferredImage.AnidbAnimeID };
                defImagesByAnime.Add(defImages.AnimeID, defImages);
            }

            switch (preferredImage.ImageType)
            {
                case ImageEntityType.Poster:
                    defImages.Poster = preferredImage.ToClient(image);
                    break;
                case ImageEntityType.Banner:
                    defImages.Banner = preferredImage.ToClient(image);
                    break;
                case ImageEntityType.Backdrop:
                    defImages.Backdrop = preferredImage.ToClient(image);
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
            return Poster;
        }

        return new() { AnimeID = AnimeID, ImageType = (int)CL_ImageEntityType.AniDB_Cover };
    }

    public CL_AniDB_Anime_DefaultImage GetFanartContractNoBlanks(CL_AniDB_Anime anime)
    {
        ArgumentNullException.ThrowIfNull(anime, nameof(anime));

        if (Backdrop != null)
        {
            return Backdrop;
        }

        var fanarts = anime.Fanarts;

        if (fanarts == null || fanarts.Count == 0)
        {
            return null;
        }

        if (fanarts.Count == 1)
        {
            return fanarts[0];
        }

        var random = new Random();

        return fanarts[random.Next(0, fanarts.Count - 1)];
    }

    public int AnimeID { get; set; }

    public CL_AniDB_Anime_DefaultImage Poster { get; set; }

    public CL_AniDB_Anime_DefaultImage Backdrop { get; set; }

    public CL_AniDB_Anime_DefaultImage Banner { get; set; }
}
