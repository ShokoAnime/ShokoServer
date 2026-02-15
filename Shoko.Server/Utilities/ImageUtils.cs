using System.Diagnostics.CodeAnalysis;
using System.IO;
using Shoko.Server.Services;

#nullable enable
namespace Shoko.Server.Utilities;

public class ImageUtils
{
    [return: NotNullIfNotNull(nameof(relativePath))]
    public static string? ResolvePath(string? relativePath)
        => !string.IsNullOrEmpty(relativePath)
            ? Path.Join(Path.TrimEndingDirectorySeparator(BaseImagesPath), relativePath)
            : null;

    public static string BaseImagesPath
        => ApplicationPaths.Instance.ImagesPath;

    public static string BaseAniDBImagesPath
        => Path.Join(BaseImagesPath, "AniDB");

    public static string BaseAniDBCharacterImagesPath
        => Path.Join(BaseImagesPath, "AniDB_Char");

    public static string BaseAniDBCreatorImagesPath
        => Path.Join(BaseImagesPath, "AniDB_Creator");

    public static string GetAniDBCharacterImagePath(int characterID)
        => Path.Join(BaseAniDBCharacterImagesPath, characterID.ToString() is { Length: > 1 } sid ? sid[..2] : characterID.ToString());

    public static string GetAniDBCreatorImagePath(int creatorID)
        => Path.Join(BaseAniDBCreatorImagesPath, creatorID.ToString() is { Length: > 1 } sid ? sid[..2] : creatorID.ToString());

    public static string GetAniDBImagePath(int animeID)
        => Path.Join(BaseAniDBImagesPath, animeID.ToString() is { Length: > 1 } sid ? sid[..2] : animeID.ToString());
}
