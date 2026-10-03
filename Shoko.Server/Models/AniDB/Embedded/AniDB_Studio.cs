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

namespace Shoko.Server.Models.AniDB;

public class AniDB_Studio : IStudio
{
    private readonly string? _imagePath;

    public int AnidbID { get; private set; }

    public string Name { get; private set; }

    #region Constructor

    public AniDB_Studio(AniDB_Creator creator)
    {
        _imagePath = creator.ImagePath;
        AnidbID = creator.CreatorID;
        Name = creator.Name;
    }

    #endregion

    #region Methods

    IEnumerable<AniDB_Anime> GetAnime() =>
        RepoFactory.AniDB_Anime_Character_Creator.GetByCreatorID(AnidbID)
        .Select(xref => xref.Anime)
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

    StudioType IStudio.StudioType => StudioType.None;

    // A view of an AniDB creator, which keeps no country and no last fetch.
    string? IStudio.CountryOfOrigin => null;

    DateTime? IStudio.LastRefreshedAt => null;

    IEnumerable<IMovie> IStudio.MovieWorks => [];

    IEnumerable<ISeries> IStudio.SeriesWorks => GetAnime();

    IEnumerable<IMetadata> IStudio.Works => GetAnime();

    #endregion
}

public class AniDB_Studio_For_Anime : AniDB_Studio, IStudio<ISeries>
{
    private readonly AniDB_Anime_Staff _xref;

    public int ParentID { get; private set; }

    public ISeries Parent { get; private set; }

    #region Constructor

    public AniDB_Studio_For_Anime(AniDB_Anime_Staff xref, AniDB_Creator creator, AniDB_Anime parent) : base(creator)
    {
        _xref = xref;
        ParentID = parent.AnimeID;
        Parent = parent;
    }

    #endregion

    #region IStudio Implementation

    StudioType IStudio.StudioType => _xref.Role is "Animation Work" ? StudioType.Animation : StudioType.None;

    MetadataGuid IStudio<ISeries>.ParentID => new(MetadataSource.AniDB, MetadataEntityType.Series, ParentID.ToString());

    ISeries? IStudio<ISeries>.Parent => Parent;

    #endregion
}
