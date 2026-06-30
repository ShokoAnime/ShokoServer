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

public class TMDB_Studio<TEntity> : IStudio<TEntity> where TEntity : IMetadata<int>, IEntityMetadata
{
    public int ID { get; private set; }

    public int ParentID { get; private set; }

    public string Name { get; private set; }

    public TEntity Parent { get; private set; }

    #region Constructor

    public TMDB_Studio(TMDB_Company company, TEntity parent)
    {
        ID = company.TmdbCompanyID;
        ParentID = parent.ID;
        Name = company.Name;
        Parent = parent;
    }

    #endregion

    #region Methods

    IEnumerable<TMDB_Movie> GetMovies() =>
        RepoFactory.TMDB_Company_Entity.GetByTmdbEntityTypeAndCompanyID(DataEntityType.Movie, ID)
        .Select(xref => xref.GetTmdbMovie())
        .WhereNotNull();

    IEnumerable<TMDB_Show> GetShows() =>
        RepoFactory.TMDB_Company_Entity.GetByTmdbEntityTypeAndCompanyID(DataEntityType.Show, ID)
        .Select(xref => xref.GetTmdbShow())
        .WhereNotNull();

    IEnumerable<IMetadata> GetWorks() =>
        RepoFactory.TMDB_Company_Entity.GetByTmdbCompanyID(ID)
        .Select(xref => xref.GetTmdbEntity() as IMetadata<int>)
        .WhereNotNull();

    #endregion

    #region IMetadata Implementation

    DataEntityType IMetadata.EntityType => DataEntityType.Studio;

    DataSource IMetadata.Source => DataSource.TMDB;

    #endregion

    #region IWithImages Implementation

    public IImageCrossReference? DefaultPrimaryImageCrossReference => ((IWithImages)this).GetImageCrossReferences(new() { ImageSource = DataSource.TMDB, ImageType = ImageEntityType.Primary }).FirstOrDefault();

    #endregion

    #region IStudio Implementation

    string? IStudio.OriginalName => null;

    StudioType IStudio.StudioType => StudioType.None;

    IEnumerable<IMovie> IStudio.MovieWorks => GetMovies();

    IEnumerable<ISeries> IStudio.SeriesWorks => GetShows();

    IEnumerable<IMetadata> IStudio.Works => GetWorks();

    TEntity? IStudio<TEntity>.Parent => Parent;

    #endregion
}
