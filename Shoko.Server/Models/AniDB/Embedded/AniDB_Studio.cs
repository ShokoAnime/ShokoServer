using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

namespace Shoko.Server.Models.AniDB;

public class AniDB_Studio(AniDB_Creator creator) : IStudio
{
    private readonly string? _imagePath = creator.ImagePath;

    public int AnidbID { get; } = creator.CreatorID;

    public string Name { get; } = creator.Name;

    #region Methods

    /// <summary>
    /// The anime the company has a studio role on, as the anime read their studios.
    /// </summary>
    /// <returns>Each anime once.</returns>
    IEnumerable<AniDB_Anime> GetAnime() =>
        RepoFactory.AniDB_Anime_Staff.GetByCreatorID(AnidbID)
        .Where(xref => xref.RoleType is CreatorRoleType.Studio)
        .Select(xref => xref.AnimeID)
        .Distinct()
        .Select(animeID => RepoFactory.AniDB_Anime.GetByAnimeID(animeID))
        .WhereNotNull();

    #endregion

    #region IMetadata Implementation

    MetadataGuid IMetadata.ID => new(MetadataSource.AniDB, MetadataEntityType.Studio, AnidbID.ToString());

    #endregion

    #region IWithImages Implementation

    public IImageCrossReference? DefaultPrimaryImageCrossReference => !string.IsNullOrEmpty(_imagePath) && IImageManager.GetIDForImageSourceAndResourceID(MetadataSource.AniDB, _imagePath) is { } posterID
        ? (this as IWithImages).GetImageCrossReferences(new() { ImageSource = MetadataSource.AniDB, ImageType = ImageEntityType.Primary }).FirstOrDefault(xref => xref.ImageID == posterID)
        : null;

    #endregion

    #region IStudio Implementation

    string? IStudio.OriginalName => null;

    // AniDB's own entries are written whole with their anime.
    bool IStudio.IsStub => false;

    StudioType IStudio.StudioType => StudioType.None;

    // A view of an AniDB creator, which keeps no country and no last fetch.
    string? IStudio.CountryOfOrigin => null;

    DateTime? IStudio.LastRefreshedAt => null;

    IEnumerable<IMovie> IStudio.MovieWorks => [];

    IEnumerable<ISeries> IStudio.SeriesWorks => GetAnime();

    IEnumerable<IMetadata> IStudio.Works => GetAnime();

    #endregion
}

public class AniDB_Studio_For_Anime(AniDB_Anime_Staff _xref, AniDB_Creator creator, AniDB_Anime parent) : AniDB_Studio(creator), IStudio<ISeries>
{
    public int ParentID { get; } = parent.AnimeID;

    public ISeries Parent { get; } = parent;

    #region IStudio Implementation

    StudioType IStudio.StudioType => _xref.Role is "Animation Work" ? StudioType.Animation : StudioType.None;

    MetadataGuid IStudio<ISeries>.ParentID => new(MetadataSource.AniDB, MetadataEntityType.Series, ParentID.ToString());

    ISeries? IStudio<ISeries>.Parent => Parent;

    #endregion
}
