using Shoko.Models.Enums;
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
            DataSourceType.TMDB => RepoFactory.TMDB_Image.GetByID(ImageID)?.GetImageMetadata(true, ImageType),
            _ => null,
        };
    }
}
