using System;
using System.ComponentModel.DataAnnotations.Schema;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public abstract class TMDB_Image_Entity
{
    #region Properties

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_Image_EntityID { get; set; }

    /// <summary>
    /// TMDB Remote File Name.
    /// </summary>
    public string RemoteFileName { get; set; } = string.Empty;

    /// <summary>
    /// TMDB Image Type.
    /// </summary>
    public ImageEntityType ImageType { get; set; }

    /// <summary>
    /// TMDB Entity Type.
    /// </summary>
    [NotMapped] // Discriminators cannot be mapped. They are automatically set from the type
    public ForeignEntityType TmdbEntityType { get; set; }

    /// <summary>
    /// TMDB Entity ID.
    /// </summary>
    public int TmdbEntityID { get; set; }

    /// <summary>
    /// Used for ordering the companies for the entity.
    /// </summary>
    public int Ordering { get; set; }

    /// <summary>
    /// Used for ordering the entities for the image.
    /// </summary>
    public DateOnly? ReleasedAt { get; set; }

    #endregion

    public bool Populate(int index, DateOnly? releasedAt = null)
    {
        var updated = TMDB_Image_EntityID is 0;
        if (Ordering != index)
        {
            Ordering = index;
            updated = true;
        }

        if (ReleasedAt != releasedAt)
        {
            ReleasedAt = releasedAt;
            updated = true;
        }

        return updated;
    }

    public TMDB_Image? GetTmdbImage(bool ofType = true) =>
        RepoFactory.TMDB_Image.GetByRemoteFileName(RemoteFileName) is { } image
            ? (ofType ? image.GetImageMetadata(imageType: ImageType) : image)
            : null;
}

public class TMDB_Image_Collection : TMDB_Image_Entity;
public class TMDB_Image_Company : TMDB_Image_Entity;
public class TMDB_Image_Movie : TMDB_Image_Entity;
public class TMDB_Image_TVShow : TMDB_Image_Entity;
public class TMDB_Image_Season : TMDB_Image_Entity;
public class TMDB_Image_Episode : TMDB_Image_Entity;
public class TMDB_Image_Person : TMDB_Image_Entity;
public class TMDB_Image_Character : TMDB_Image_Entity;
public class TMDB_Image_Network : TMDB_Image_Entity;
public class TMDB_Image_Studio : TMDB_Image_Entity;
