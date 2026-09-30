using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Repositories;

#pragma warning disable CS0618
namespace Shoko.Server.Models.TMDB;

public class TMDB_Studio<TEntity> : IStudio<TEntity> where TEntity : IMetadata, IEntityMetadata
{
    public int TmdbID { get; private set; }

    public int ParentID { get; private set; }

    public string Name { get; private set; }

    public TEntity Parent { get; private set; }

    #region Constructor

    public TMDB_Studio(TMDB_Company company, TEntity parent)
    {
        TmdbID = company.TmdbCompanyID;
        ParentID = parent.Id;
        Name = company.Name;
        Parent = parent;
    }

    #endregion

    #region Methods

    IEnumerable<TMDB_Movie> GetMovies() =>
        RepoFactory.TMDB_Company_Entity.GetByTmdbEntityTypeAndCompanyID(MetadataEntityType.Movie, TmdbID)
        .Select(xref => xref.GetTmdbMovie())
        .WhereNotNull();

    IEnumerable<TMDB_Show> GetShows() =>
        RepoFactory.TMDB_Company_Entity.GetByTmdbEntityTypeAndCompanyID(MetadataEntityType.Series, TmdbID)
        .Select(xref => xref.GetTmdbShow())
        .WhereNotNull();

    IEnumerable<IMetadata> GetWorks() =>
        RepoFactory.TMDB_Company_Entity.GetByTmdbCompanyID(TmdbID)
        .Select(xref => xref.GetTmdbEntity() as IMetadata)
        .WhereNotNull();

    #endregion

    #region IMetadata Implementation

    MetadataGuid IMetadata.ID => new(MetadataSource.TMDB, MetadataEntityType.Studio, TmdbID.ToString());

    #endregion

    #region IWithImages Implementation

    public IImageCrossReference? DefaultPrimaryImageCrossReference => ((IWithImages)this).GetImageCrossReferences(new() { ImageSource = MetadataSource.TMDB, ImageType = ImageEntityType.Primary }).FirstOrDefault();

    #endregion

    #region IStudio Implementation

    string? IStudio.OriginalName => null;

    StudioType IStudio.StudioType => StudioType.None;

    IEnumerable<IMovie> IStudio.MovieWorks => GetMovies();

    IEnumerable<ISeries> IStudio.SeriesWorks => GetShows();

    IEnumerable<IMetadata> IStudio.Works => GetWorks();

    MetadataGuid IStudio<TEntity>.ParentID => Parent.ID;

    TEntity? IStudio<TEntity>.Parent => Parent;

    #endregion
}
