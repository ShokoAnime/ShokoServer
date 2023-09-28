using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.ImageDownload;

public class ImageUtils
{
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

    public static string GetBaseTraktImagesPath()
    {
        var dirPath = Path.Combine(GetBaseImagesPath(), "Trakt");
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

    public static string GetTraktImagePath()
    {
        var dirPath = GetBaseTraktImagesPath();
        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return dirPath;
    }

    public static string? GetLocalPath(CL_ImageEntityType imageEntityType, int imageId, bool resolve = false)
        => GetLocalPath(imageEntityType.ToServerSource(), imageEntityType.ToServerType(), imageId, resolve);

    public static string? GetLocalPath(DataSourceType dataSource, ImageEntityType imageType, int imageId, bool resolve = false)
    {
        switch (dataSource)
        {
            case DataSourceType.AniDB:
                switch (imageType)
                {
                    case ImageEntityType.Character:
                        var anidbCharacter = RepoFactory.AniDB_Character.GetByCharID(imageId);
                        if (string.IsNullOrEmpty(anidbCharacter?.PicName))
                            return null;

                        var anidbCharacterPath = anidbCharacter.GetPosterPath();
                        if (resolve && !File.Exists(anidbCharacterPath))
                            return null;

                        return anidbCharacterPath;

                    case ImageEntityType.Person:
                        var anidbCreator = RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(imageId);
                        if (string.IsNullOrEmpty(anidbCreator?.PicName))
                            return null;

                        var anidbCreatorPath = anidbCreator.GetPosterPath();
                        if (resolve && !File.Exists(anidbCreatorPath))
                            return null;

                        return anidbCreatorPath;

                    case ImageEntityType.Poster:
                        var anidbAnime = RepoFactory.AniDB_Anime.GetByAnimeID(imageId);
                        if (string.IsNullOrEmpty(anidbAnime?.Picname))
                            return null;

                        var anidbAnimePath = anidbAnime.PosterPath;
                        if (resolve && !File.Exists(anidbAnimePath))
                            return null;

                        return anidbAnimePath;
                }
                break;

            case DataSourceType.Shoko:
                switch (imageType)
                {
                    case ImageEntityType.Character:
                        var shokoCharacter = RepoFactory.AnimeCharacter.GetByID(imageId);
                        if (string.IsNullOrEmpty(shokoCharacter?.ImagePath))
                            return null;

                        var shokoCharacterPath = shokoCharacter.GetFullImagePath();
                        if (resolve && !File.Exists(shokoCharacterPath))
                            return null;

                        return shokoCharacterPath;

                    case ImageEntityType.Person:
                        var shokoCreator = RepoFactory.AnimeStaff.GetByID(imageId);
                        if (string.IsNullOrEmpty(shokoCreator?.ImagePath))
                            return null;

                        var shokoCreatorPath = shokoCreator.GetFullImagePath();
                        if (resolve && !File.Exists(shokoCreatorPath))
                            return null;

                        return shokoCreatorPath;
                }
                break;

            case DataSourceType.TMDB:
                var tmdbImagePath = RepoFactory.TMDB_Image.GetByID(imageId)?.LocalPath;
                if (string.IsNullOrEmpty(tmdbImagePath) || (resolve && !File.Exists(tmdbImagePath)))
                    return null;

                return tmdbImagePath;

            case DataSourceType.TvDB:
                switch (imageType)
                {
                    case ImageEntityType.Backdrop:
                        var tvdbBackdrop = RepoFactory.TvDB_ImageFanart.GetByID(imageId);
                        if (string.IsNullOrEmpty(tvdbBackdrop?.BannerPath))
                            return null;

                        var tvdbBackdropPath = tvdbBackdrop.GetFullImagePath();
                        if (resolve && !File.Exists(tvdbBackdropPath))
                            return null;

                        return tvdbBackdropPath;

                    case ImageEntityType.Banner:
                        var tvdbBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(imageId);
                        if (string.IsNullOrEmpty(tvdbBanner?.BannerPath))
                            return null;

                        var tvdbBannerPath = tvdbBanner.GetFullImagePath();
                        if (resolve && !File.Exists(tvdbBannerPath))
                            return null;

                        return tvdbBannerPath;

                    case ImageEntityType.Poster:
                        var tvdbPoster = RepoFactory.TvDB_ImagePoster.GetByID(imageId);
                        if (string.IsNullOrEmpty(tvdbPoster?.BannerPath))
                            return null;

                        var tvdbPosterPath = tvdbPoster.GetFullImagePath();
                        if (resolve && !File.Exists(tvdbPosterPath))
                            return null;

                        return tvdbPosterPath;

                    case ImageEntityType.Thumbnail:
                        var tvdbEpisode = RepoFactory.TvDB_Episode.GetByID(imageId);
                        if (string.IsNullOrEmpty(tvdbEpisode?.Filename))
                            return null;

                        var tvdbEpisodePath = tvdbEpisode.GetFullImagePath();
                        if (resolve && !File.Exists(tvdbEpisodePath))
                            return null;

                        return tvdbEpisodePath;
                }
                break;
        }

        return null;
    }

    public static int? GetRandomImageID(CL_ImageEntityType imageType)
        => GetRandomImageID(imageType.ToServerSource(), imageType.ToServerType());

    public static int? GetRandomImageID(DataSourceType dataSource, ImageEntityType imageType)
    {
        return dataSource switch
        {
            DataSourceType.AniDB => imageType switch
            {
                ImageEntityType.Poster => RepoFactory.AniDB_Anime.GetAll()
                    .Where(a => a?.PosterPath != null && !a.GetAllTags().Contains("18 restricted"))
                    .GetRandomElement()?.AnimeID,
                ImageEntityType.Character => RepoFactory.AniDB_Anime.GetAll()
                    .Where(a => a != null && !a.GetAllTags().Contains("18 restricted"))
                    .SelectMany(a => a.GetAnimeCharacters()).Select(a => a.GetCharacter()).Where(a => a != null)
                    .GetRandomElement()?.AniDB_CharacterID,
                ImageEntityType.Person => RepoFactory.AniDB_Anime.GetAll()
                    .Where(a => a != null && !a.GetAllTags().Contains("18 restricted"))
                    .SelectMany(a => a.GetAnimeCharacters())
                    .SelectMany(a => RepoFactory.AniDB_Character_Seiyuu.GetByCharID(a.CharID))
                    .Select(a => RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(a.SeiyuuID)).Where(a => a != null)
                    .GetRandomElement()?.AniDB_SeiyuuID,
                _ => null,
            },
            DataSourceType.Shoko => imageType switch
            {
                ImageEntityType.Character => RepoFactory.AniDB_Anime.GetAll()
                    .Where(a => a != null && !a.GetAllTags().Contains("18 restricted"))
                    .SelectMany(a => RepoFactory.CrossRef_Anime_Staff.GetByAnimeID(a.AnimeID))
                    .Where(a => a.RoleType == (int)StaffRoleType.Seiyuu && a.RoleID.HasValue)
                    .Select(a => RepoFactory.AnimeCharacter.GetByID(a.RoleID!.Value))
                    .GetRandomElement()?.CharacterID,
                ImageEntityType.Person => RepoFactory.AniDB_Anime.GetAll()
                    .Where(a => a != null && !a.GetAllTags().Contains("18 restricted"))
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

        foreach (var animeID in animeIDs)
            SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);

        return true;
    }
}
