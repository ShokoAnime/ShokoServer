using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Repositories;
using TMDbLib.Objects.General;

#pragma warning disable CS0618
#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_Company : IStudio
{
    #region Properties

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_CompanyID { get; set; }

    /// <summary>
    /// TMDB Company ID.
    /// </summary>
    public int TmdbCompanyID { get; set; }

    /// <summary>
    /// Main name of the company on TMDB.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The country the company originates from.
    /// </summary>
    public string CountryOfOrigin { get; set; } = string.Empty;

    #endregion

    #region Constructors

    public TMDB_Company() { }

    public TMDB_Company(int companyId)
    {
        TmdbCompanyID = companyId;
    }

    #endregion

    #region Methods

    public bool Populate(ProductionCompany company)
    {
        var updated = false;
        if (!string.IsNullOrEmpty(company.Name) && !string.Equals(company.Name, Name))
        {
            Name = company.Name;
            updated = true;
        }
        if (!string.IsNullOrEmpty(company.OriginCountry) && !string.Equals(company.OriginCountry, CountryOfOrigin))
        {
            CountryOfOrigin = company.OriginCountry;
            updated = true;
        }
        return updated;
    }

    public IReadOnlyList<TMDB_Company_Entity> GetTmdbCompanyCrossReferences() =>
        RepoFactory.TMDB_Company_Entity.GetByTmdbCompanyID(TmdbCompanyID);

    public IReadOnlyList<IEntityMetadata> GetTmdbEntities() =>
        GetTmdbCompanyCrossReferences()
            .Select(xref => xref.GetTmdbEntity())
            .WhereNotNull()
            .ToList();

    public IReadOnlyList<IEntityMetadata> GetTmdbShows() =>
        GetTmdbCompanyCrossReferences()
            .Select(xref => xref.GetTmdbShow())
            .WhereNotNull()
            .ToList();

    public IReadOnlyList<IEntityMetadata> GetTmdbMovies() =>
        GetTmdbCompanyCrossReferences()
            .Select(xref => xref.GetTmdbMovie())
            .WhereNotNull()
            .ToList();

    #endregion

    #region IMetadata Implementation

    DataEntityType IMetadata.EntityType => DataEntityType.Company;

    int IMetadata<int>.ID => TmdbCompanyID;

    DataSource IMetadata.Source => DataSource.TMDB;

    #endregion

    #region IWithImages Implementation

    public IImage? GetPreferredImageForType(ImageEntityType imageType)
        => GetImages(imageType: imageType).FirstOrDefault(image => image.IsPreferred);

    public IImageCrossReference? GetPreferredImageCrossReferenceForType(ImageEntityType imageType)
        => GetImageCrossReferences(imageType: imageType).FirstOrDefault(xref => xref.IsPreferred);

    public IReadOnlyList<IImage> GetImages(DataSource? imageSource = null, ImageEntityType? imageType = null, DataSource? xrefSource = null, bool? isEnabled = null, bool? isDesired = null, bool primaryImage = false)
        => ISystemService.StaticServices.GetRequiredService<IImageManager>()
            .GetImagesForEntity(this, imageSource, imageType, xrefSource, isEnabled, isDesired, primaryImage);

    public IReadOnlyList<IImageCrossReference> GetImageCrossReferences(DataSource? imageSource = null, ImageEntityType? imageType = null, DataSource? xrefSource = null, bool? isEnabled = null, bool? isDesired = null)
        => ISystemService.StaticServices.GetRequiredService<IImageManager>()
            .GetImageCrossReferencesForEntity(this, imageSource, imageType, xrefSource, isEnabled, isDesired);

    #endregion

    #region IWithPrimaryImage Implementation

    public IImage? DefaultPrimaryImage => GetImages(imageSource: DataSource.TMDB, imageType: ImageEntityType.Primary).FirstOrDefault();

    public IImageCrossReference? DefaultPrimaryImageCrossReference => GetImageCrossReferences(imageSource: DataSource.TMDB, imageType: ImageEntityType.Primary).FirstOrDefault();

    #endregion

    #region IStudio Implementation

    string? IStudio.OriginalName => null;

    StudioType IStudio.StudioType => StudioType.Animation;

    IEnumerable<IMovie> IStudio.MovieWorks => [];

    IEnumerable<ISeries> IStudio.SeriesWorks => [];

    IEnumerable<IMetadata> IStudio.Works => [];

    #endregion
}
