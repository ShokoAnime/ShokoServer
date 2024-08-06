using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Utilities;

public class ImageUtils
{
    private static ISchedulerFactory? __schedulerFactory = null;

    private static ISchedulerFactory _schedulerFactory
        => __schedulerFactory ??= Utils.ServiceContainer.GetService<ISchedulerFactory>()!;

    public static string? ResolvePath(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return null;

        var filePath = Path.Join(Path.TrimEndingDirectorySeparator(GetBaseImagesPath()), relativePath);
        var dirPath = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return filePath;
    }

    public static string GetBaseImagesPath()
    {
        var settings = Utils.SettingsProvider?.GetSettings();
        var baseDirPath = !string.IsNullOrEmpty(settings?.ImagesPath) ?
            Path.Combine(Utils.ApplicationPath, settings.ImagesPath) : Utils.DefaultImagePath;
        if (!Directory.Exists(baseDirPath))
            Directory.CreateDirectory(baseDirPath);

        return baseDirPath;
    }

    public static string GetBaseAniDBImagesPath()
    {
        var dirPath = Path.Combine(GetBaseImagesPath(), "AniDB");
        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return dirPath;
    }

    public static string GetBaseAniDBCharacterImagesPath()
    {
        var dirPath = Path.Combine(GetBaseImagesPath(), "AniDB_Char");
        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return dirPath;
    }

    public static string GetBaseAniDBCreatorImagesPath()
    {
        var dirPath = Path.Combine(GetBaseImagesPath(), "AniDB_Creator");

        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return dirPath;
    }

    public static string GetBaseTvDBImagesPath()
    {
        var dirPath = Path.Combine(GetBaseImagesPath(), "TvDB");
        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return dirPath;
    }

    public static string GetImagesTempFolder()
    {
        var dirPath = Path.Combine(GetBaseImagesPath(), "_Temp_");

        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return dirPath;
    }

    public static string GetAniDBCharacterImagePath(int charID)
    {
        var sid = charID.ToString();
        var subFolder = sid.Length == 1 ? sid : sid[..2];
        var dirPath = Path.Combine(GetBaseAniDBCharacterImagesPath(), subFolder);
        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return dirPath;
    }

    public static string GetAniDBCreatorImagePath(int creatorID)
    {
        var sid = creatorID.ToString();
        var subFolder = sid.Length == 1 ? sid : sid[..2];
        var dirPath = Path.Combine(GetBaseAniDBCreatorImagesPath(), subFolder);
        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return dirPath;
    }

    public static string GetAniDBImagePath(int animeID)
    {
        var sid = animeID.ToString();
        var subFolder = sid.Length == 1 ? sid : sid[..2];
        var dirPath = Path.Combine(GetBaseAniDBImagesPath(), subFolder);
        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return dirPath;
    }

    public static string GetTvDBImagePath()
    {
        var dirPath = GetBaseTvDBImagesPath();
        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return dirPath;
    }

    public static IImageMetadata? GetImageMetadata(CL_ImageEntityType imageEntityType, int imageId)
        => GetImageMetadata(imageEntityType.ToServerSource(), imageEntityType.ToServerType(), imageId);

    public static IImageMetadata? GetImageMetadata(DataSourceEnum dataSource, ImageEntityType imageType, int imageId)
        => GetImageMetadata(dataSource.ToDataSourceType(), imageType, imageId);

    public static IImageMetadata? GetImageMetadata(DataSourceType dataSource, ImageEntityType imageType, int imageId)
        => dataSource switch
        {
            DataSourceType.AniDB => imageType switch
            {
                ImageEntityType.Character => RepoFactory.AniDB_Character.GetByCharID(imageId)?.GetImageMetadata(),
                ImageEntityType.Person => RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(imageId)?.GetImageMetadata(),
                ImageEntityType.Poster => RepoFactory.AniDB_Anime.GetByAnimeID(imageId)?.GetImageMetadata(),
                _ => null,
            },
            DataSourceType.Shoko => imageType switch
            {
                ImageEntityType.Character => RepoFactory.AnimeCharacter.GetByID(imageId)?.GetImageMetadata(),
                ImageEntityType.Person => RepoFactory.AnimeStaff.GetByID(imageId)?.GetImageMetadata(),
                _ => null,
            },
            DataSourceType.TMDB => RepoFactory.TMDB_Image.GetByID(imageId),
            DataSourceType.TvDB => imageType switch
            {
                ImageEntityType.Backdrop => RepoFactory.TvDB_ImageFanart.GetByTvDBID(imageId)?.GetImageMetadata(),
                ImageEntityType.Banner => RepoFactory.TvDB_ImageWideBanner.GetByTvDBID(imageId)?.GetImageMetadata(),
                ImageEntityType.Poster => RepoFactory.TvDB_ImagePoster.GetByTvDBID(imageId)?.GetImageMetadata(),
                ImageEntityType.Thumbnail => RepoFactory.TvDB_Episode.GetByTvDBID(imageId)?.GetImageMetadata(),
                _ => null,
            },
            _ => null,
        };

    public static int? GetRandomImageID(CL_ImageEntityType imageType)
        => GetRandomImageID(imageType.ToServerSource(), imageType.ToServerType());

    public static int? GetRandomImageID(DataSourceType dataSource, ImageEntityType imageType)
    {
        return dataSource switch
        {
            DataSourceType.AniDB => imageType switch
            {
                ImageEntityType.Poster => RepoFactory.AniDB_Anime.GetAll()
                    .Where(a => a?.PosterPath is not null && !a.GetAllTags().Contains("18 restricted"))
                    .GetRandomElement()?.AnimeID,
                ImageEntityType.Character => RepoFactory.AniDB_Anime.GetAll()
                    .Where(a => a is not null && !a.GetAllTags().Contains("18 restricted"))
                    .SelectMany(a => a.Characters).Select(a => a.GetCharacter()).WhereNotNull()
                    .GetRandomElement()?.AniDB_CharacterID,
                ImageEntityType.Person => RepoFactory.AniDB_Anime.GetAll()
                    .Where(a => a is not null && !a.GetAllTags().Contains("18 restricted"))
                    .SelectMany(a => a.Characters)
                    .SelectMany(a => RepoFactory.AniDB_Character_Seiyuu.GetByCharID(a.CharID))
                    .Select(a => RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(a.SeiyuuID)).WhereNotNull()
                    .GetRandomElement()?.AniDB_SeiyuuID,
                _ => null,
            },
            DataSourceType.Shoko => imageType switch
            {
                ImageEntityType.Character => RepoFactory.AniDB_Anime.GetAll()
                    .Where(a => a is not null && !a.GetAllTags().Contains("18 restricted"))
                    .SelectMany(a => RepoFactory.CrossRef_Anime_Staff.GetByAnimeID(a.AnimeID))
                    .Where(a => a.RoleType == (int)StaffRoleType.Seiyuu && a.RoleID.HasValue)
                    .Select(a => RepoFactory.AnimeCharacter.GetByID(a.RoleID!.Value))
                    .GetRandomElement()?.CharacterID,
                ImageEntityType.Person => RepoFactory.AniDB_Anime.GetAll()
                    .Where(a => a is not null && !a.GetAllTags().Contains("18 restricted"))
                    .SelectMany(a => RepoFactory.CrossRef_Anime_Staff.GetByAnimeID(a.AnimeID))
                    .Select(a => RepoFactory.AnimeStaff.GetByID(a.StaffID))
                    .GetRandomElement()?.StaffID,
                _ => null,
            },
            DataSourceType.TMDB => RepoFactory.TMDB_Image.GetByType(imageType)
                .GetRandomElement()?.TMDB_ImageID,
            // TvDB doesn't allow H content, so we get to skip the check!
            DataSourceType.TvDB => imageType switch
            {
                ImageEntityType.Backdrop => RepoFactory.TvDB_ImageFanart.GetAll()
                    .GetRandomElement()?.TvDB_ImageFanartID,
                ImageEntityType.Banner => RepoFactory.TvDB_ImageWideBanner.GetAll()
                    .GetRandomElement()?.TvDB_ImageWideBannerID,
                ImageEntityType.Poster => RepoFactory.TvDB_ImagePoster.GetAll()
                    .GetRandomElement()?.TvDB_ImagePosterID,
                ImageEntityType.Thumbnail => RepoFactory.TvDB_Episode.GetAll()
                    .GetRandomElement()?.Id,
                _ => null,
            },
            _ => null,
        };
    }

    public static SVR_AnimeSeries? GetFirstSeriesForImage(DataSourceType dataSource, ImageEntityType imageType, int imageID)
    {
        switch (dataSource)
        {
            case DataSourceType.AniDB:
                switch (imageType)
                {
                    case ImageEntityType.Poster:
                        return RepoFactory.AnimeSeries.GetByAnimeID(imageID);
                }

                return null;

            case DataSourceType.TMDB:
                var tmdbImage = RepoFactory.TMDB_Image.GetByID(imageID);
                if (tmdbImage == null || !tmdbImage.TmdbMovieID.HasValue)
                    return null;

                if (tmdbImage.TmdbMovieID.HasValue)
                {
                    var movieXref = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByTmdbMovieID(tmdbImage.TmdbMovieID.Value).FirstOrDefault();
                    if (movieXref == null)
                        return null;

                    return RepoFactory.AnimeSeries.GetByAnimeID(movieXref.AnidbAnimeID);
                }

                if (tmdbImage.TmdbShowID.HasValue)
                {
                    var showXref = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByTmdbShowID(tmdbImage.TmdbShowID.Value).FirstOrDefault();
                    if (showXref == null)
                        return null;

                    return RepoFactory.AnimeSeries.GetByAnimeID(showXref.AnidbAnimeID);
                }

                return null;

            case DataSourceType.TvDB:
                switch (imageType)
                {
                    case ImageEntityType.Backdrop:
                        var tvdbFanart = RepoFactory.TvDB_ImageFanart.GetByID(imageID);
                        if (tvdbFanart == null)
                            return null;

                        var fanartXRef = RepoFactory.CrossRef_AniDB_TvDB.GetByTvDBID(tvdbFanart.SeriesID).FirstOrDefault();
                        if (fanartXRef == null)
                            return null;

                        return RepoFactory.AnimeSeries.GetByAnimeID(fanartXRef.AniDBID);

                    case ImageEntityType.Banner:
                        var tvdbWideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(imageID);
                        if (tvdbWideBanner == null)
                            return null;

                        var bannerXRef = RepoFactory.CrossRef_AniDB_TvDB.GetByTvDBID(tvdbWideBanner.SeriesID).FirstOrDefault();
                        if (bannerXRef == null)
                            return null;

                        return RepoFactory.AnimeSeries.GetByAnimeID(bannerXRef.AniDBID);

                    case ImageEntityType.Poster:
                        var tvdbPoster = RepoFactory.TvDB_ImagePoster.GetByID(imageID);
                        if (tvdbPoster == null)
                            return null;

                        var coverXRef = RepoFactory.CrossRef_AniDB_TvDB.GetByTvDBID(tvdbPoster.SeriesID).FirstOrDefault();
                        if (coverXRef == null)
                            return null;

                        return RepoFactory.AnimeSeries.GetByAnimeID(coverXRef.AniDBID);

                    case ImageEntityType.Thumbnail:
                        var tvdbEpisode = RepoFactory.TvDB_Episode.GetByID(imageID);
                        if (tvdbEpisode == null)
                            return null;

                        var episodeXRef = RepoFactory.CrossRef_AniDB_TvDB.GetByTvDBID(tvdbEpisode.SeriesID).FirstOrDefault();
                        if (episodeXRef == null)
                            return null;

                        return RepoFactory.AnimeSeries.GetByAnimeID(episodeXRef.AniDBID);
                }
                return null;

            default:
                return null;
        }
    }

    public static bool SetEnabled(DataSourceType dataSource, ImageEntityType imageType, int imageId, bool value = true)
    {
        var animeIDs = new HashSet<int>();
        switch (dataSource)
        {
            case DataSourceType.AniDB:
                switch (imageType)
                {
                    case ImageEntityType.Poster:
                        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(imageId);
                        if (anime == null)
                            return false;

                        anime.ImageEnabled = value ? 1 : 0;
                        RepoFactory.AniDB_Anime.Save(anime);
                        break;

                    default:
                        return false;
                }
                break;

            case DataSourceType.TMDB:
                var tmdbImage = RepoFactory.TMDB_Image.GetByID(imageId);
                if (tmdbImage == null)
                    return false;

                tmdbImage.IsEnabled = value;
                RepoFactory.TMDB_Image.Save(tmdbImage);
                if (tmdbImage.TmdbShowID.HasValue)
                    foreach (var xref in RepoFactory.CrossRef_AniDB_TMDB_Show.GetByTmdbShowID(tmdbImage.TmdbShowID.Value))
                        animeIDs.Add(xref.AnidbAnimeID);
                if (tmdbImage.TmdbMovieID.HasValue)
                    foreach (var xref in RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByTmdbMovieID(tmdbImage.TmdbMovieID.Value))
                        animeIDs.Add(xref.AnidbAnimeID);
                break;

            case DataSourceType.TvDB:
                switch (imageType)
                {
                    case ImageEntityType.Backdrop:
                        var fanart = RepoFactory.TvDB_ImageFanart.GetByID(imageId);
                        if (fanart == null)
                            return false;

                        fanart.Enabled = value ? 1 : 0;
                        RepoFactory.TvDB_ImageFanart.Save(fanart);
                        foreach (var xref in RepoFactory.CrossRef_AniDB_TvDB.GetByTvDBID(fanart.SeriesID))
                            animeIDs.Add(xref.AniDBID);
                        break;

                    case ImageEntityType.Banner:
                        var banner = RepoFactory.TvDB_ImageWideBanner.GetByID(imageId);
                        if (banner == null)
                            return false;

                        banner.Enabled = value ? 1 : 0;
                        RepoFactory.TvDB_ImageWideBanner.Save(banner);
                        foreach (var xref in RepoFactory.CrossRef_AniDB_TvDB.GetByTvDBID(banner.SeriesID))
                            animeIDs.Add(xref.AniDBID);
                        break;

                    case ImageEntityType.Poster:
                        var poster = RepoFactory.TvDB_ImagePoster.GetByID(imageId);
                        if (poster == null)
                            return false;

                        poster.Enabled = value ? 1 : 0;
                        RepoFactory.TvDB_ImagePoster.Save(poster);
                        foreach (var xref in RepoFactory.CrossRef_AniDB_TvDB.GetByTvDBID(poster.SeriesID))
                            animeIDs.Add(xref.AniDBID);
                        break;

                    default:
                        return false;
                }
                break;

            default:
                return false;
        }

        var scheduler = _schedulerFactory.GetScheduler().ConfigureAwait(false).GetAwaiter().GetResult();
        foreach (var animeID in animeIDs)
            scheduler.StartJob<RefreshAnimeStatsJob>(a => a.AnimeID = animeID).GetAwaiter().GetResult();

        return true;
    }
}
