
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_Anime_PreferredImage
{
    public int AniDB_Anime_PreferredImageID { get; set; }

    public int AnidbAnimeID { get; set; }

    public int ImageID { get; set; }

    public ImageEntityType ImageType { get; set; }

    public DataSourceType ImageSource { get; set; }

    public AniDB_Anime_PreferredImage() { }

    public AniDB_Anime_PreferredImage(int anidbAnimeId, ImageEntityType imageType)
    {
        AnidbAnimeID = anidbAnimeId;
        ImageType = imageType;
    }

    public IImageMetadata? GetImageMetadata()
    {
        return ImageSource switch
        {
            DataSourceType.AniDB when ImageType is ImageEntityType.Poster => RepoFactory.AniDB_Anime.GetByAnimeID(AnidbAnimeID) is { } anime ? anime.GetImageMetadata(true) : null,
            DataSourceType.TMDB => RepoFactory.TMDB_Image.GetByID(ImageID)?.GetImageMetadata(true),
            DataSourceType.TvDB => ImageType switch
            {
                ImageEntityType.Backdrop => RepoFactory.TvDB_ImageFanart.GetByID(ImageID)?.GetImageMetadata(true),
                ImageEntityType.Banner => RepoFactory.TvDB_ImageWideBanner.GetByID(ImageID)?.GetImageMetadata(true),
                ImageEntityType.Poster => RepoFactory.TvDB_ImagePoster.GetByID(ImageID)?.GetImageMetadata(true),
                _ => null,
            },
            _ => null,
        };
    }

    public IImageEntity? GetImageEntity()
        => ImageSource switch
        {
            DataSourceType.TMDB => ImageType switch
            {
                ImageEntityType.Backdrop =>
                    RepoFactory.TMDB_Image.GetByID(ImageID)?.ToClientFanart(),
                ImageEntityType.Poster =>
                    RepoFactory.TMDB_Image.GetByID(ImageID)?.ToClientPoster(),
                _ => null,
            },
            DataSourceType.TvDB => ImageType switch
            {
                ImageEntityType.Backdrop =>
                    RepoFactory.TvDB_ImageFanart.GetByID(ImageID),
                ImageEntityType.Banner =>
                    RepoFactory.TvDB_ImageWideBanner.GetByID(ImageID),
                ImageEntityType.Poster =>
                    RepoFactory.TvDB_ImagePoster.GetByID(ImageID),
                _ => null,
            },
            _ => null,
        };
}
