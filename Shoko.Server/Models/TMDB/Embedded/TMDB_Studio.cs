using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

#nullable enable
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

    #region IWithImages Implementation

    public IImage? GetPreferredImageForType(ImageEntityType imageType)
        => GetImages(imageType: imageType).FirstOrDefault(image => image.IsPreferred);

    public IImageCrossReference? GetPreferredImageCrossReferenceForType(ImageEntityType imageType)
        => GetImageCrossReferences(imageType: imageType).FirstOrDefault(xref => xref.IsPreferred);

    public IReadOnlyList<IImage> GetImages(DataSource? imageSource = null, ImageEntityType? imageType = null, DataSource? xrefSource = null, bool? isEnabled = null, bool? isDesired = null, bool primaryImage = false)
        => Utils.ServiceContainer.GetRequiredService<IImageManager>()
            .GetImagesForEntity(this, imageSource, imageType, xrefSource, isEnabled, isDesired, primaryImage);

    public IReadOnlyList<IImageCrossReference> GetImageCrossReferences(DataSource? imageSource = null, ImageEntityType? imageType = null, DataSource? xrefSource = null, bool? isEnabled = null, bool? isDesired = null)
        => Utils.ServiceContainer.GetRequiredService<IImageManager>()
            .GetImageCrossReferencesForEntity(this, imageSource, imageType, xrefSource, isEnabled, isDesired);

    #endregion

    #endregion

    #region IWithPrimaryImage Implementation

    public IImage? DefaultPrimaryImage => GetImages(imageSource: DataSource.TMDB, imageType: ImageEntityType.Primary).FirstOrDefault();

    public IImageCrossReference? DefaultPrimaryImageCrossReference => GetImageCrossReferences(imageSource: DataSource.TMDB, imageType: ImageEntityType.Primary).FirstOrDefault();

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
