using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_Studio : IStudio<ISeries>
{
    private readonly AniDB_Anime_Staff _xref;

    private readonly AniDB_Creator _creator;

    public int ID { get; private set; }

    public int ParentID { get; private set; }

    public string Name { get; private set; }

    public ISeries Parent { get; private set; }

    #region Constructor

    public AniDB_Studio(AniDB_Anime_Staff xref, AniDB_Creator creator, AniDB_Anime parent)
    {
        _xref = xref;
        _creator = creator;
        ID = creator.CreatorID;
        ParentID = parent.AnimeID;
        Name = creator.Name;
        Parent = parent;
    }

    #endregion

    #region Methods

    IEnumerable<AniDB_Anime> GetAnime() =>
        RepoFactory.AniDB_Anime_Character_Creator.GetByCreatorID(ID)
        .Select(xref => xref.Anime)
        .WhereNotNull();

    #endregion

    #region IMetadata Implementation

    DataSource IMetadata.Source => DataSource.AniDB;

    #endregion

    #region IWithPortraitImage Implementation

    IImage? IWithPortraitImage.PortraitImage => _creator.GetImageMetadata();

    #endregion

    #region IStudio Implementation

    string? IStudio.OriginalName => null;

    StudioType IStudio.StudioType => _xref.Role is "Animation Work" ? StudioType.Animation : StudioType.None;

    IEnumerable<IMovie> IStudio.MovieWorks => [];

    IEnumerable<ISeries> IStudio.SeriesWorks => GetAnime();

    IEnumerable<IMetadata> IStudio.Works => GetAnime();

    IMetadata<int>? IStudio.Parent => Parent;

    ISeries? IStudio<ISeries>.ParentOfType => Parent;

    #endregion
}
