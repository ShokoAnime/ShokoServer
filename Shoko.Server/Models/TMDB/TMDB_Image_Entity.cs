using System;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_Image_Entity
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

    #region Constructors

    public TMDB_Image_Entity() { }

    public TMDB_Image_Entity(string remoteFileName, ImageEntityType imageType, ForeignEntityType entityType, int entityId)
    {
        RemoteFileName = remoteFileName;
        ImageType = imageType;
        TmdbEntityType = entityType;
        TmdbEntityID = entityId;
    }

    #endregion

    #region Methods

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

    public IEntityMetadata? GetTmdbEntity() =>
        TmdbEntityType switch
        {
            ForeignEntityType.Movie => RepoFactory.TMDB_Movie.GetByTmdbMovieID(TmdbEntityID),
            ForeignEntityType.Episode => RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(TmdbEntityID),
            ForeignEntityType.Season => RepoFactory.TMDB_Season.GetByTmdbSeasonID(TmdbEntityID),
            ForeignEntityType.Show => RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbEntityID),
            ForeignEntityType.Collection => RepoFactory.TMDB_Collection.GetByTmdbCollectionID(TmdbEntityID),
            // ForeignEntityType.Network => null,
            // ForeignEntityType.Company => null,
            ForeignEntityType.Person => RepoFactory.TMDB_Person.GetByTmdbPersonID(TmdbEntityID),
            _ => null,
        };

    public TMDB_Movie? GetTmdbMovie() => TmdbEntityType == ForeignEntityType.Movie
        ? RepoFactory.TMDB_Movie.GetByTmdbMovieID(TmdbEntityID)
        : null;

    public TMDB_Episode? GetTmdbEpisode() => TmdbEntityType == ForeignEntityType.Episode
        ? RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(TmdbEntityID)
        : null;

    public TMDB_Season? GetTmdbSeason() => TmdbEntityType == ForeignEntityType.Season
        ? RepoFactory.TMDB_Season.GetByTmdbSeasonID(TmdbEntityID)
        : null;

    public TMDB_Show? GetTmdbShow() => TmdbEntityType == ForeignEntityType.Show
        ? RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbEntityID)
        : null;

    public TMDB_Collection? GetTmdbCollection() => TmdbEntityType == ForeignEntityType.Collection
        ? RepoFactory.TMDB_Collection.GetByTmdbCollectionID(TmdbEntityID)
        : null;

    public TMDB_Network? GetTmdbNetwork() => TmdbEntityType == ForeignEntityType.Network
        ? RepoFactory.TMDB_Network.GetByTmdbNetworkID(TmdbEntityID)
        : null;

    public TMDB_Company? GetTmdbCompany() => TmdbEntityType == ForeignEntityType.Company
        ? RepoFactory.TMDB_Company.GetByTmdbCompanyID(TmdbEntityID)
        : null;

    public TMDB_Person? GetTmdbPerson() => TmdbEntityType == ForeignEntityType.Person
        ? RepoFactory.TMDB_Person.GetByTmdbPersonID(TmdbEntityID)
        : null;

    #endregion
}
