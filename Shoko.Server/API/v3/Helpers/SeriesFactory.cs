using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Quartz;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.TvDB;
using Shoko.Server.Utilities;
using AniDBAnimeType = Shoko.Models.Enums.AnimeType;
using AniDBEpisodeType = Shoko.Models.Enums.EpisodeType;
using Image = Shoko.Server.API.v3.Models.Common.Image;
using Series = Shoko.Server.API.v3.Models.Shoko.Series;

namespace Shoko.Server.API.v3.Helpers;

public class SeriesFactory
{
    private readonly HttpContext _context;
    private readonly CrossRef_Anime_StaffRepository _crossRefAnimeStaffRepository;
    private readonly AniDBTitleHelper _titleHelper;
    private readonly AnimeCharacterRepository _animeCharacterRepository;
    private readonly AnimeStaffRepository _animeStaffRepository;
    private readonly CustomTagRepository _customTagRepository;
    private readonly AniDB_TagRepository _aniDBTagRepository;
    private readonly AniDB_Anime_TagRepository _aniDBAnimeTagRepository;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly JobFactory _jobFactory;

    public SeriesFactory(IHttpContextAccessor context, ISchedulerFactory schedulerFactory, JobFactory jobFactory, AniDBTitleHelper titleHelper)
    {
        _schedulerFactory = schedulerFactory;
        _jobFactory = jobFactory;
        _titleHelper = titleHelper;
        _context = context.HttpContext;
        _crossRefAnimeStaffRepository = RepoFactory.CrossRef_Anime_Staff;
        _animeCharacterRepository = RepoFactory.AnimeCharacter;
        _animeStaffRepository = RepoFactory.AnimeStaff;
        _customTagRepository = RepoFactory.CustomTag;
        _aniDBTagRepository = RepoFactory.AniDB_Tag;
        _aniDBAnimeTagRepository = RepoFactory.AniDB_Anime_Tag;
    }

    public Series GetSeries(SVR_AnimeSeries ser, bool randomiseImages = false, HashSet<DataSource> includeDataFrom = null)
    {
        var uid = _context.GetUser()?.JMMUserID ?? 0;
        var anime = ser.AniDB_Anime;
        var animeType = (AniDBAnimeType)anime.AnimeType;

        var result = new Series();
        AddBasicAniDBInfo(result, anime);

        var ael = ser.AnimeEpisodes;

        result.IDs = GetIDs(ser);
        result.HasCustomName = !string.IsNullOrEmpty(ser.SeriesNameOverride);
        result.Images = GetDefaultImages(ser, randomiseImages);
        result.AirsOn = animeType == AniDBAnimeType.TVSeries || animeType == AniDBAnimeType.Web ? GetAirsOnDaysOfWeek(ael) : new();

        result.Name = ser.SeriesName;
        result.Sizes = ModelHelper.GenerateSeriesSizes(ael, uid);
        result.Size = result.Sizes.Local.Credits + result.Sizes.Local.Episodes + result.Sizes.Local.Others + result.Sizes.Local.Parodies +
                      result.Sizes.Local.Specials + result.Sizes.Local.Trailers;

        result.Created = ser.DateTimeCreated.ToUniversalTime();
        result.Updated = ser.DateTimeUpdated.ToUniversalTime();

        if (includeDataFrom?.Contains(DataSource.AniDB) ?? false)
        {
            result._AniDB = GetAniDB(anime, ser);
        }
        if (includeDataFrom?.Contains(DataSource.TvDB) ?? false)
            result._TvDB = GetTvDBInfo(ser);

        return result;
    }

    /// <summary>
    /// Get the most recent days in the week the show airs on.
    /// </summary>
    /// <param name="animeEpisodes">Optionally pass in the episodes so we don't have to fetch them.</param>
    /// <param name="includeThreshold">Threshold of episodes to include in the calculation.</param>
    /// <returns></returns>
    public List<DayOfWeek> GetAirsOnDaysOfWeek(IEnumerable<SVR_AnimeEpisode> animeEpisodes, int includeThreshold = 24)
    {
        var now = DateTime.Now;
        var filteredEpisodes = animeEpisodes
            .Select(episode =>
            {
                var aniDB = episode.AniDB_Episode;
                var airDate = aniDB.GetAirDateAsDate();
                return (episode, aniDB, airDate);
            })
            .Where(tuple =>
            {
                // We ignore all other types except the "normal" type.
                if ((AniDBEpisodeType)tuple.aniDB.EpisodeType != AniDBEpisodeType.Episode)
                    return false;

                // We ignore any unknown air dates and dates in the future.
                if (!tuple.airDate.HasValue || tuple.airDate.Value > now)
                    return false;

                return true;
            })
            .ToList();

        // Threshold used to filter out outliners, e.g. a weekday that only happens
        // once or twice for whatever reason, or when a show gets an early preview,
        // an episode moving, etc...
        var outlierThreshold = Math.Min((int)Math.Ceiling(filteredEpisodes.Count / 12D), 4);
        return filteredEpisodes
            .OrderByDescending(tuple => tuple.aniDB.EpisodeNumber)
            // We check up to the `x` last aired episodes to get a grasp on which days
            // it airs on. This helps reduce variance in days for long-running
            // shows, such as One Piece, etc...
            .Take(includeThreshold)
            .Select(tuple => tuple.airDate.Value.DayOfWeek)
            .GroupBy(weekday => weekday)
            .Where(list => list.Count() > outlierThreshold)
            .Select(list => list.Key)
            .OrderBy(weekday => weekday)
            .ToList();
    }

    private void AddBasicAniDBInfo(Series result, SVR_AniDB_Anime anime)
    {
        if (anime == null)
        {
            return;
        }

        result.Links = new();
        if (!string.IsNullOrEmpty(anime.Site_EN))
            foreach (var site in anime.Site_EN.Split('|'))
                result.Links.Add(new() { Type = "source", Name = "Official Site (EN)", URL = site });

        if (!string.IsNullOrEmpty(anime.Site_JP))
            foreach (var site in anime.Site_JP.Split('|'))
                result.Links.Add(new() { Type = "source", Name = "Official Site (JP)", URL = site });

        if (!string.IsNullOrEmpty(anime.Wikipedia_ID))
            result.Links.Add(new() { Type = "wiki", Name = "Wikipedia (EN)", URL = $"https://en.wikipedia.org/{anime.Wikipedia_ID}" });

        if (!string.IsNullOrEmpty(anime.WikipediaJP_ID))
            result.Links.Add(new() { Type = "wiki", Name = "Wikipedia (JP)", URL = $"https://en.wikipedia.org/{anime.WikipediaJP_ID}" });

        if (!string.IsNullOrEmpty(anime.CrunchyrollID))
            result.Links.Add(new() { Type = "streaming", Name = "Crunchyroll", URL = $"https://crunchyroll.com/anime/{anime.CrunchyrollID}" });

        if (!string.IsNullOrEmpty(anime.FunimationID))
            result.Links.Add(new() { Type = "streaming", Name = "Funimation", URL = anime.FunimationID });

        if (!string.IsNullOrEmpty(anime.HiDiveID))
            result.Links.Add(new() { Type = "streaming", Name = "HiDive", URL = $"https://www.hidive.com/{anime.HiDiveID}" });

        if (anime.AllCinemaID.HasValue && anime.AllCinemaID.Value > 0)
            result.Links.Add(new() { Type = "foreign-metadata", Name = "allcinema", URL = $"https://allcinema.net/cinema/{anime.AllCinemaID.Value}" });

        if (anime.AnisonID.HasValue && anime.AnisonID.Value > 0)
            result.Links.Add(new() { Type = "foreign-metadata", Name = "Anison", URL = $"https://anison.info/data/program/{anime.AnisonID.Value}.html" });

        if (anime.SyoboiID.HasValue && anime.SyoboiID.Value > 0)
            result.Links.Add(new() { Type = "foreign-metadata", Name = "syoboi", URL = $"https://cal.syoboi.jp/tid/{anime.SyoboiID.Value}/time" });

        if (anime.BangumiID.HasValue && anime.BangumiID.Value > 0)
            result.Links.Add(new() { Type = "foreign-metadata", Name = "bangumi", URL = $"https://bgm.tv/subject/{anime.BangumiID.Value}" });

        if (anime.LainID.HasValue && anime.LainID.Value > 0)
            result.Links.Add(new() { Type = "foreign-metadata", Name = ".lain", URL = $"http://lain.gr.jp/mediadb/media/{anime.LainID.Value}" });

        if (anime.ANNID.HasValue && anime.ANNID.Value > 0)
            result.Links.Add(new() { Type = "english-metadata", Name = "AnimeNewsNetwork", URL = $"https://www.animenewsnetwork.com/encyclopedia/anime.php?id={anime.ANNID.Value}" });

        if (anime.VNDBID.HasValue && anime.VNDBID.Value > 0)
            result.Links.Add(new() { Type = "english-metadata", Name = "VNDB", URL = $"https://vndb.org/v{anime.VNDBID.Value}" });

        var vote = RepoFactory.AniDB_Vote.GetByEntityAndType(anime.AnimeID, AniDBVoteType.Anime) ??
                   RepoFactory.AniDB_Vote.GetByEntityAndType(anime.AnimeID, AniDBVoteType.AnimeTemp);
        if (vote != null)
        {
            var voteType = (AniDBVoteType)vote.VoteType == AniDBVoteType.Anime ? "Permanent" : "Temporary";
            result.UserRating = new Rating
            {
                Value = (decimal)Math.Round(vote.VoteValue / 100D, 1),
                MaxValue = 10,
                Type = voteType,
                Source = "User"
            };
        }
    }

    public async Task<bool> QueueAniDBRefresh(ISchedulerFactory schedulerFactory, JobFactory jobFactory,
        int animeID, bool force, bool downloadRelations, bool createSeriesEntry, bool immediate = false,
        bool cacheOnly = false)
    {
        if (animeID == 0) return false;
        if (immediate)
        {
            var command = jobFactory.CreateJob<GetAniDBAnimeJob>(c =>
            {
                c.AnimeID = animeID;
                c.DownloadRelations = downloadRelations;
                c.ForceRefresh = force;
                c.CacheOnly = !force && cacheOnly;
                c.CreateSeriesEntry = createSeriesEntry;
            });

            try
            {
                return await command.Process() != null;
            }
            catch
            {
                return false;
            }
        }

        var scheduler = await schedulerFactory.GetScheduler();
        await scheduler.StartJob<GetAniDBAnimeJob>(c =>
        {
            c.AnimeID = animeID;
            c.DownloadRelations = downloadRelations;
            c.ForceRefresh = force;
            c.CacheOnly = !force && cacheOnly;
            c.CreateSeriesEntry = createSeriesEntry;
        });
        return false;
    }

    public async Task<bool> QueueTvDBRefresh(int tvdbID, bool force, bool immediate = false)
    {
        if (immediate)
        {
            try
            {
                var job = _jobFactory.CreateJob<GetTvDBSeriesJob>(c =>
                {
                    c.TvDBSeriesID = tvdbID;
                    c.ForceRefresh = force;
                });
                return await job.Process() != null;
            }
            catch
            {
                return false;
            }
        }

        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<GetTvDBSeriesJob>(c =>
        {
            c.TvDBSeriesID = tvdbID;
            c.ForceRefresh = force;
        });
        return false;
    }

    public static SeriesIDs GetIDs(SVR_AnimeSeries ser)
    {
        // Shoko
        var ids = new SeriesIDs
        {
            ID = ser.AnimeSeriesID,
            ParentGroup = ser.AnimeGroupID,
            TopLevelGroup = ser.TopLevelAnimeGroup?.AnimeGroupID ?? 0
        };

        // AniDB
        var anidbId = ser.AniDB_Anime?.AnimeID;
        if (anidbId.HasValue)
        {
            ids.AniDB = anidbId.Value;
        }

        // TvDB
        var tvdbIds = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(ser.AniDB_ID);
        if (tvdbIds.Any())
        {
            ids.TvDB.AddRange(tvdbIds.Select(a => a.TvDBID).Distinct());
        }

        // TODO: Cache the rest of these, so that they don't severely slow down the API

        // TMDB
        // TODO: make this able to support more than one, in fact move it to its own and remove CrossRef_Other
        var tmdbId = RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(ser.AniDB_ID, CrossRefType.MovieDB);
        if (tmdbId != null && int.TryParse(tmdbId.CrossRefID, out var movieID))
        {
            ids.TMDB.Add(movieID);
        }

        // Trakt
        // var traktIds = RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(ser.AniDB_ID).Select(a => a.TraktID)
        //     .Distinct().ToList();
        // if (traktIds.Any()) ids.TraktTv.AddRange(traktIds);

        // MAL
        var malIds = RepoFactory.CrossRef_AniDB_MAL.GetByAnimeID(ser.AniDB_ID).Select(a => a.MALID).Distinct()
            .ToList();
        if (malIds.Any())
        {
            ids.MAL.AddRange(malIds);
        }

        // TODO: AniList later
        return ids;
    }

    public static Image GetDefaultImage(int anidbId, ImageSizeType imageSizeType,
        ImageEntityType? imageEntityType = null)
    {
        var defaultImage = imageEntityType.HasValue
            ? RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeTypeAndImageEntityType(anidbId,
                imageSizeType, imageEntityType.Value)
            : RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(anidbId, imageSizeType);
        return defaultImage != null
            ? new Image(defaultImage.ImageParentID, (ImageEntityType)defaultImage.ImageParentType, true)
            : null;
    }

    public Images GetDefaultImages(SVR_AnimeSeries ser, bool randomiseImages = false)
    {
        var images = new Images();
        var random = _context.Items["Random"] as Random;
        var allImages = GetArt(ser.AniDB_ID);

        var poster = randomiseImages
            ? allImages.Posters.GetRandomElement(random)
            : GetDefaultImage(ser.AniDB_ID, ImageSizeType.Poster) ?? allImages.Posters.FirstOrDefault();
        if (poster != null)
        {
            images.Posters.Add(poster);
        }

        var fanart = randomiseImages
            ? allImages.Fanarts.GetRandomElement(random)
            : GetDefaultImage(ser.AniDB_ID, ImageSizeType.Fanart) ?? allImages.Fanarts.FirstOrDefault();
        if (fanart != null)
        {
            images.Fanarts.Add(fanart);
        }

        var banner = randomiseImages
            ? allImages.Banners.GetRandomElement(random)
            : GetDefaultImage(ser.AniDB_ID, ImageSizeType.WideBanner) ?? allImages.Banners.FirstOrDefault();
        if (banner != null)
        {
            images.Banners.Add(banner);
        }

        return images;
    }

    public List<Series.TvDB> GetTvDBInfo(SVR_AnimeSeries ser)
    {
        var ael = ser.AnimeEpisodes;
        return RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(ser.AniDB_ID)
            .Select(xref => RepoFactory.TvDB_Series.GetByTvDBID(xref.TvDBID))
            .Select(tvdbSer => GetTvDB(tvdbSer, ser, ael))
            .ToList();
    }

    public static async Task AddSeriesVote(ISchedulerFactory schedulerFactory, SVR_AnimeSeries ser, int userID, Vote vote)
    {
        var voteType = (vote.Type?.ToLowerInvariant() ?? "") switch
        {
            "temporary" => AniDBVoteType.AnimeTemp,
            "permanent" => AniDBVoteType.Anime,
            _ => ser.AniDB_Anime?.GetFinishedAiring() ?? false ? AniDBVoteType.Anime : AniDBVoteType.AnimeTemp
        };

        var dbVote = RepoFactory.AniDB_Vote.GetByEntityAndType(ser.AniDB_ID, AniDBVoteType.AnimeTemp) ??
                     RepoFactory.AniDB_Vote.GetByEntityAndType(ser.AniDB_ID, AniDBVoteType.Anime);

        if (dbVote == null)
        {
            dbVote = new AniDB_Vote { EntityID = ser.AniDB_ID };
        }

        dbVote.VoteValue = (int)Math.Floor(vote.GetRating(1000));
        dbVote.VoteType = (int)voteType;

        RepoFactory.AniDB_Vote.Save(dbVote);

        var scheduler = await schedulerFactory.GetScheduler();
        await scheduler.StartJob<VoteAniDBAnimeJob>(c =>
            {
                c.AnimeID = ser.AniDB_ID;
                c.VoteType = voteType;
                c.VoteValue = vote.GetRating();
            }
        );
    }

    public static Images GetArt(int animeID, bool includeDisabled = false)
    {
        var images = new Images();
        AddAniDBPoster(images, animeID);
        AddTvDBImages(images, animeID, includeDisabled);
        // AddMovieDBImages(ctx, images, animeID, includeDisabled);
        return images;
    }

    private static void AddAniDBPoster(Images images, int animeID)
    {
        images.Posters.Add(GetAniDBPoster(animeID));
    }

    public static Image GetAniDBPoster(int animeID)
    {
        var defaultImage = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(animeID,
            ImageSizeType.Poster);
        var preferred = defaultImage != null && defaultImage.ImageParentType == (int)ImageEntityType.AniDB_Cover;
        return new Image(animeID, ImageEntityType.AniDB_Cover, preferred);
    }

    private static void AddTvDBImages(Images images, int animeID, bool includeDisabled = false)
    {
        var tvdbIDs = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(animeID).ToList();

        var defaultFanart =
            RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeTypeAndImageEntityType(animeID,
                ImageSizeType.Fanart, ImageEntityType.TvDB_FanArt);
        var fanarts = tvdbIDs.SelectMany(a => RepoFactory.TvDB_ImageFanart.GetBySeriesID(a.TvDBID)).ToList();
        images.Fanarts.AddRange(fanarts.Where(a => includeDisabled || a.Enabled != 0).Select(a =>
        {
            var preferred = defaultFanart != null && defaultFanart.ImageParentID == a.TvDB_ImageFanartID;
            return new Image(a.TvDB_ImageFanartID, ImageEntityType.TvDB_FanArt, preferred, a.Enabled == 0);
        }));

        var defaultBanner =
            RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeTypeAndImageEntityType(animeID,
                ImageSizeType.WideBanner, ImageEntityType.TvDB_Banner);
        var banners = tvdbIDs.SelectMany(a => RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(a.TvDBID)).ToList();
        images.Banners.AddRange(banners.Where(a => includeDisabled || a.Enabled != 0).Select(a =>
        {
            var preferred = defaultBanner != null && defaultBanner.ImageParentID == a.TvDB_ImageWideBannerID;
            return new Image(a.TvDB_ImageWideBannerID, ImageEntityType.TvDB_Banner, preferred, a.Enabled == 0);
        }));

        var defaultPoster =
            RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeTypeAndImageEntityType(animeID,
                ImageSizeType.Poster, ImageEntityType.TvDB_Cover);
        var posters = tvdbIDs.SelectMany(a => RepoFactory.TvDB_ImagePoster.GetBySeriesID(a.TvDBID)).ToList();
        images.Posters.AddRange(posters.Where(a => includeDisabled || a.Enabled != 0).Select(a =>
        {
            var preferred = defaultPoster != null && defaultPoster.ImageParentID == a.TvDB_ImagePosterID;
            return new Image(a.TvDB_ImagePosterID, ImageEntityType.TvDB_Cover, preferred, a.Enabled == 0);
        }));
    }

    private static void AddMovieDBImages(Images images, int animeID, bool includeDisabled = false)
    {
        var moviedb = RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(animeID, CrossRefType.MovieDB);

        var moviedbPosters = moviedb == null
            ? new List<MovieDB_Poster>()
            : RepoFactory.MovieDB_Poster.GetByMovieID(int.Parse(moviedb.CrossRefID));
        var defaultPoster =
            RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeTypeAndImageEntityType(animeID,
                ImageSizeType.Poster, ImageEntityType.MovieDB_Poster);
        images.Posters.AddRange(moviedbPosters.Where(a => includeDisabled || a.Enabled != 0).Select(a =>
        {
            var preferred = defaultPoster != null && defaultPoster.ImageParentID == a.MovieDB_PosterID;
            return new Image(a.MovieDB_PosterID, ImageEntityType.MovieDB_Poster, preferred, a.Enabled == 1);
        }));

        var moviedbFanarts = moviedb == null
            ? new List<MovieDB_Fanart>()
            : RepoFactory.MovieDB_Fanart.GetByMovieID(int.Parse(moviedb.CrossRefID));
        var defaultFanart =
            RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeTypeAndImageEntityType(animeID,
                ImageSizeType.Fanart, ImageEntityType.MovieDB_FanArt);
        images.Fanarts.AddRange(moviedbFanarts.Where(a => includeDisabled || a.Enabled != 0).Select(a =>
        {
            var preferred = defaultFanart != null && defaultFanart.ImageParentID == a.MovieDB_FanartID;
            return new Image(a.MovieDB_FanartID, ImageEntityType.MovieDB_FanArt, preferred, a.Enabled == 1);
        }));
    }

    public Series.AniDB GetAniDB(SVR_AniDB_Anime anime, SVR_AnimeSeries series = null, bool includeTitles = true)
    {
        series ??= RepoFactory.AnimeSeries.GetByAnimeID(anime.AnimeID);
        var seriesTitle = series?.SeriesName ?? anime.PreferredTitle;
        var result = new Series.AniDB
        {
            ID = anime.AnimeID,
            ShokoID = series?.AnimeSeriesID,
            Type = GetAniDBSeriesType(anime.AnimeType),
            Title = seriesTitle,
            Titles = includeTitles
                ? anime.Titles.Select(title => new Title
                    {
                        Name = title.Title,
                        Language = title.LanguageCode,
                        Type = title.TitleType,
                        Default = string.Equals(title.Title, seriesTitle),
                        Source = "AniDB"
                    }
                ).ToList()
                : null,
            Description = anime.Description,
            Restricted = anime.Restricted == 1,
            Poster = GetAniDBPoster(anime.AnimeID),
            EpisodeCount = anime.EpisodeCountNormal,
            Rating = new Rating
            {
                Source = "AniDB", Value = anime.Rating, MaxValue = 1000, Votes = anime.VoteCount
            },
            UserApproval = null,
            Relation = null,
        };

        if (anime.AirDate.HasValue) result.AirDate = anime.AirDate.Value;
        if (anime.EndDate.HasValue) result.EndDate = anime.EndDate.Value;

        return result;
    }
    
    public Series.AniDB GetAniDB(ResponseAniDBTitles.Anime result, SVR_AnimeSeries series = null, bool includeTitles = false)
    {
        if (series == null)
        {
            series = RepoFactory.AnimeSeries.GetByAnimeID(result.AnimeID);
        }

        var anime = series != null ? series.AniDB_Anime : RepoFactory.AniDB_Anime.GetByAnimeID(result.AnimeID);

        var animeTitle = series?.SeriesName ?? anime?.PreferredTitle ?? result.PreferredTitle;
        var anidb = new Series.AniDB
        {
            ID = result.AnimeID,
            ShokoID = series?.AnimeSeriesID,
            Type = GetAniDBSeriesType(anime?.AnimeType),
            Title = animeTitle,
            Titles = includeTitles
                ? result.Titles.Select(title => new Title
                    {
                        Language = title.LanguageCode,
                        Name = title.Title,
                        Type = title.TitleType,
                        Default = string.Equals(title.Title, animeTitle),
                        Source = "AniDB"
                    }
                ).ToList()
                : null,
            Description = anime?.Description,
            Restricted = anime is { Restricted: 1 },
            EpisodeCount = anime?.EpisodeCount,
            Poster = GetAniDBPoster(result.AnimeID),
        };
        if (anime?.AirDate != null) anidb.AirDate = anime.AirDate.Value;
        if (anime?.EndDate != null) anidb.EndDate = anime.EndDate.Value;
        return anidb;
    }
    
    public Series.AniDB GetAniDB(SVR_AniDB_Anime_Relation relation, SVR_AnimeSeries series = null, bool includeTitles = true)
    {
        series ??= RepoFactory.AnimeSeries.GetByAnimeID(relation.RelatedAnimeID);
        var result = new Series.AniDB
        {
            ID = relation.RelatedAnimeID,
            ShokoID = series?.AnimeSeriesID,
            Poster = GetAniDBPoster(relation.RelatedAnimeID),
            Rating = null,
            UserApproval = null,
            Relation = ((IRelatedMetadata)relation).RelationType,
        };
        SetAniDBTitles(result, relation, series, includeTitles);
        return result;
    }

    private void SetAniDBTitles(Series.AniDB aniDB, AniDB_Anime_Relation relation, SVR_AnimeSeries series, bool includeTitles)
    {
        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(relation.RelatedAnimeID);
        if (anime is not null)
        {
            aniDB.Type = GetAniDBSeriesType(anime.AnimeType);
            aniDB.Title = series?.SeriesName ?? anime.PreferredTitle;
            aniDB.Titles = includeTitles
                ? anime.Titles.Select(
                    title => new Title
                    {
                        Name = title.Title,
                        Language = title.LanguageCode,
                        Type = title.TitleType,
                        Default = string.Equals(title.Title, aniDB.Title),
                        Source = "AniDB"
                    }
                ).ToList()
                : null;
            aniDB.Description = anime.Description;
            aniDB.Restricted = anime.Restricted == 1;
            aniDB.EpisodeCount = anime.EpisodeCountNormal;
            return;
        }

        var result = _titleHelper.SearchAnimeID(relation.RelatedAnimeID);
        if (result != null)
        {
            aniDB.Type = SeriesType.Unknown;
            aniDB.Title = result.PreferredTitle;
            aniDB.Titles = includeTitles
                ? result.Titles.Select(
                    title => new Title
                    {
                        Language = title.LanguageCode,
                        Name = title.Title,
                        Type = title.TitleType,
                        Default = string.Equals(title.Title, aniDB.Title),
                        Source = "AniDB"
                    }
                ).ToList()
                : null;
            aniDB.Description = null;
            // If the other anime is present we assume they're of the same kind. Be it restricted or unrestricted.
            anime = RepoFactory.AniDB_Anime.GetByAnimeID(relation.AnimeID);
            aniDB.Restricted = anime is not null && anime.Restricted == 1;
            return;
        }

        aniDB.Type = SeriesType.Unknown;
        aniDB.Titles = includeTitles ? new List<Title>() : null;
        aniDB.Restricted = false;
    }

    public Series.AniDB GetAniDB(AniDB_Anime_Similar similar, SVR_AnimeSeries series = null, bool includeTitles = true)
    {
        series ??= RepoFactory.AnimeSeries.GetByAnimeID(similar.SimilarAnimeID);
        var result = new Series.AniDB
        {
            ID = similar.SimilarAnimeID,
            ShokoID = series?.AnimeSeriesID,
            Poster = GetAniDBPoster(similar.SimilarAnimeID),
            Rating = null,
            UserApproval = new Rating
            {
                Value = new Vote(similar.Approval, similar.Total).GetRating(100),
                MaxValue = 100,
                Votes = similar.Total,
                Source = "AniDB",
                Type = "User Approval"
            },
            Relation = null,
            Restricted = false,
        };
        SetAniDBTitles(result, similar, series, includeTitles);
        return result;
    }

    private void SetAniDBTitles(Series.AniDB aniDB, AniDB_Anime_Similar similar, SVR_AnimeSeries series, bool includeTitles)
    {
        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(similar.SimilarAnimeID);
        if (anime is not null)
        {
            aniDB.Type = GetAniDBSeriesType(anime.AnimeType);
            aniDB.Title = series?.SeriesName ?? anime.PreferredTitle;
            aniDB.Titles = includeTitles
                ? anime.Titles.Select(
                    title => new Title
                    {
                        Name = title.Title,
                        Language = title.LanguageCode,
                        Type = title.TitleType,
                        Default = string.Equals(title.Title, aniDB.Title),
                        Source = "AniDB"
                    }
                ).ToList()
                : null;
            aniDB.Description = anime.Description;
            aniDB.Restricted = anime.Restricted == 1;
            return;
        }

        var result = _titleHelper.SearchAnimeID(similar.SimilarAnimeID);
        if (result != null)
        {
            aniDB.Type = SeriesType.Unknown;
            aniDB.Title = result.PreferredTitle;
            aniDB.Titles = includeTitles
                ? result.Titles.Select(
                    title => new Title
                    {
                        Language = title.LanguageCode,
                        Name = title.Title,
                        Type = title.TitleType,
                        Default = string.Equals(title.Title, aniDB.Title),
                        Source = "AniDB"
                    }
                ).ToList()
                : null;
            aniDB.Description = null;
            // If the other anime is present we assume they're of the same kind. Be it restricted or unrestricted.
            anime = RepoFactory.AniDB_Anime.GetByAnimeID(similar.AnimeID);
            aniDB.Restricted = anime is not null && anime.Restricted == 1;
            return;
        }

        aniDB.Type = SeriesType.Unknown;
        aniDB.Title = null;
        aniDB.Titles = includeTitles ? new List<Title>() : null;
        aniDB.Description = null;
        aniDB.Restricted = false;
    }

    /// <summary>
    /// Cast is aggregated, and therefore not in each provider
    /// </summary>
    /// <param name="animeID"></param>
    /// <param name="roleTypes"></param>
    /// <returns></returns>
    public List<Role> GetCast(int animeID, HashSet<Role.CreatorRoleType> roleTypes = null)
    {
        var roles = new List<Role>();
        var xrefAnimeStaff = _crossRefAnimeStaffRepository.GetByAnimeID(animeID);
        foreach (var xref in xrefAnimeStaff)
        {
            // Filter out any roles that are not of the desired type.
            if (roleTypes != null && !roleTypes.Contains((Role.CreatorRoleType)xref.RoleType))
                continue;

            var character = xref.RoleID.HasValue ? _animeCharacterRepository.GetByID(xref.RoleID.Value) : null;
            var staff = _animeStaffRepository.GetByID(xref.StaffID);
            if (staff == null)
                continue;

            var role = new Role
            {
                Character =
                    character != null
                        ? new Role.Person
                        {
                            Name = character.Name,
                            AlternateName = character.AlternateName,
                            Image = new Image(character.CharacterID, ImageEntityType.Character),
                            Description = character.Description
                        }
                        : null,
                Staff = new Role.Person
                {
                    Name = staff.Name,
                    AlternateName = staff.AlternateName,
                    Description = staff.Description,
                    Image = staff.ImagePath != null ? new Image(staff.StaffID, ImageEntityType.Staff) : null
                },
                RoleName = (Role.CreatorRoleType)xref.RoleType,
                RoleDetails = xref.Role
            };
            roles.Add(role);
        }

        return roles;
    }

    public List<Tag> GetTags(SVR_AniDB_Anime anime, TagFilter.Filter filter,
        bool excludeDescriptions = false, bool orderByName = false, bool onlyVerified = true)
    {
        // Only get the user tags if we don't exclude it (false == false), or if we invert the logic and want to include it (true == true).
        IEnumerable<Tag> userTags = new List<Tag>();
        if (filter.HasFlag(TagFilter.Filter.User) == filter.HasFlag(TagFilter.Filter.Invert))
        {
            userTags = _customTagRepository.GetByAnimeID(anime.AnimeID)
                .Select(tag => new Tag(tag, excludeDescriptions));
        }

        var selectedTags = anime.GetAniDBTags(onlyVerified)
            .DistinctBy(a => a.TagName)
            .ToList();
        var tagFilter = new TagFilter<AniDB_Tag>(name => _aniDBTagRepository.GetByName(name).FirstOrDefault(), tag => tag.TagName, 
            name => new AniDB_Tag { TagNameSource = name });
        var anidbTags = tagFilter
            .ProcessTags(filter, selectedTags)
            .Select(tag => 
            {
                var xref = _aniDBAnimeTagRepository.GetByTagID(tag.TagID).FirstOrDefault(xref => xref.AnimeID == anime.AnimeID);
                return new Tag(tag, excludeDescriptions) { Weight = xref?.Weight ?? 0, IsLocalSpoiler = xref?.LocalSpoiler };
            });

        if (orderByName)
            return userTags.Concat(anidbTags)
                .OrderByDescending(tag => tag.Source)
                .ThenBy(tag => tag.Name)
                .ToList();

        return userTags.Concat(anidbTags)
            .OrderByDescending(tag => tag.Source)
            .ThenByDescending(tag => tag.Weight)
            .ThenBy(tag => tag.Name)
            .ToList();
    }
    
    public static SeriesType GetAniDBSeriesType(int? animeType)
    {
        return animeType.HasValue ? GetAniDBSeriesType((AniDBAnimeType)animeType.Value) : SeriesType.Unknown;
    }

    public static SeriesType GetAniDBSeriesType(AniDBAnimeType animeType)
    {
        switch (animeType)
        {
            default:
            case AniDBAnimeType.None:
                return SeriesType.Unknown;
            case AniDBAnimeType.TVSeries:
                return SeriesType.TV;
            case AniDBAnimeType.Movie:
                return SeriesType.Movie;
            case AniDBAnimeType.OVA:
                return SeriesType.OVA;
            case AniDBAnimeType.TVSpecial:
                return SeriesType.TVSpecial;
            case AniDBAnimeType.Web:
                return SeriesType.Web;
            case AniDBAnimeType.Other:
                return SeriesType.Other;
        }
    }
    
    public Series.TvDB GetTvDB(TvDB_Series tbdbSeries, SVR_AnimeSeries series,
        IEnumerable<SVR_AnimeEpisode> episodeList = null)
    {
        episodeList ??= series.AnimeEpisodes;

        var images = new Images();
        AddTvDBImages(images, series.AniDB_ID);

        // Aggregate stuff
        var firstEp = episodeList.FirstOrDefault(a =>
                a.AniDB_Episode != null && a.AniDB_Episode.EpisodeType == (int)AniDBEpisodeType.Episode &&
                a.AniDB_Episode.EpisodeNumber == 1)
            ?.TvDBEpisode;

        var lastEp = episodeList
            .Where(a => a.AniDB_Episode != null && a.AniDB_Episode.EpisodeType == (int)AniDBEpisodeType.Episode)
            .OrderBy(a => a.AniDB_Episode.EpisodeType)
            .ThenBy(a => a.AniDB_Episode.EpisodeNumber).LastOrDefault()
            ?.TvDBEpisode;

        var result = new Series.TvDB
        {
            ID = tbdbSeries.SeriesID,
            Description = tbdbSeries.Overview,
            Title = tbdbSeries.SeriesName,
            Posters = images.Posters,
            Fanarts = images.Fanarts,
            Banners = images.Banners,
            Season = firstEp?.SeasonNumber,
            AirDate = firstEp?.AirDate,
            EndDate = lastEp?.AirDate,
        };
        if (tbdbSeries.Rating != null)
        {
            result.Rating = new Rating { Source = "TvDB", Value = tbdbSeries.Rating.Value, MaxValue = 10 };
        }

        return result;
    }
    
    public SeriesSearchResult GetSeriesSearchResult(SeriesSearch.SearchResult<SVR_AnimeSeries> result)
    {
        var series = GetSeries(result.Result);
        var searchResult = new SeriesSearchResult
        {
            Name = series.Name,
            IDs = series.IDs,
            Size = series.Size,
            Sizes = series.Sizes,
            Created = series.Created,
            Updated = series.Updated,
            AirsOn = series.AirsOn,
            UserRating = series.UserRating,
            Images = series.Images,
            Links = series.Links,
            _AniDB = series._AniDB,
            _TvDB = series._TvDB,
            ExactMatch = result.ExactMatch,
            Index = result.Index,
            Distance = result.Distance,
            LengthDifference = result.LengthDifference,
            Match = result.Match,
        };
        return searchResult;
    }

    public SeriesWithMultipleReleasesResult GetSeriesWithMultipleReleasesResult(
        SVR_AnimeSeries animeSeries,
        bool randomiseImages = false,
        HashSet<DataSource> includeDataFrom = null,
        bool ignoreVariations = true)
    {
        var series = GetSeries(animeSeries, randomiseImages, includeDataFrom);
        var episodesWithMultipleReleases = RepoFactory.AnimeEpisode.GetWithMultipleReleases(ignoreVariations, animeSeries.AniDB_ID).Count;
        return new()
        {
            Name = series.Name,
            IDs = series.IDs,
            Size = series.Size,
            Sizes = series.Sizes,
            Created = series.Created,
            Updated = series.Updated,
            AirsOn = series.AirsOn,
            UserRating = series.UserRating,
            Images = series.Images,
            Links = series.Links,
            _AniDB = series._AniDB,
            _TvDB = series._TvDB,
            EpisodeCount = episodesWithMultipleReleases,
        };
    }
}
