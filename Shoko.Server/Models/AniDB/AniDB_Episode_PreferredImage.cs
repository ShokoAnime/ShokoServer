
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_Episode_PreferredImage
{
    public int AniDB_Episode_PreferredImageID { get; set; }

    public int AnidbAnimeID { get; set; }

    public int AnidbEpisodeID { get; set; }

    public int ImageID { get; set; }

    public ImageEntityType ImageType { get; set; }

    public DataSourceType ImageSource { get; set; }

    public AniDB_Episode_PreferredImage() { }

    public AniDB_Episode_PreferredImage(int anidbAnimeId, int anidbEpisodeId, ImageEntityType imageType)
    {
        AnidbAnimeID = anidbAnimeId;
        AnidbEpisodeID = anidbEpisodeId;
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
}
