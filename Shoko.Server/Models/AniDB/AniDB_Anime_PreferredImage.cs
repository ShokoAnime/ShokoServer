using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Metadata;
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

    public DataSource ImageSource { get; set; }

    public AniDB_Anime_PreferredImage() { }

    public AniDB_Anime_PreferredImage(int anidbAnimeId, ImageEntityType imageType)
    {
        AnidbAnimeID = anidbAnimeId;
        ImageType = imageType;
    }

    public IImage? GetImageMetadata()
    {
        return ImageSource switch
        {
            DataSource.AniDB when ImageType is ImageEntityType.Poster => RepoFactory.AniDB_Anime.GetByAnimeID(AnidbAnimeID) is { } anime ? anime.GetImageMetadata(true) : null,
            DataSource.TMDB => RepoFactory.TMDB_Image.GetByID(ImageID)?.GetImageMetadata(true, ImageType),
            _ => null,
        };
    }
}
