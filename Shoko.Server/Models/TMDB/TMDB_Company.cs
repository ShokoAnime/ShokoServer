using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Repositories;
using TMDbLib.Objects.General;

#pragma warning disable CS0618
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

    public IImageCrossReference? DefaultPrimaryImageCrossReference => ((IWithImages)this).GetImageCrossReferences(new() { ImageSource = DataSource.TMDB, ImageType = ImageEntityType.Primary }).FirstOrDefault();

    #endregion

    #region IStudio Implementation

    string? IStudio.OriginalName => null;

    StudioType IStudio.StudioType => StudioType.Animation;

    IEnumerable<IMovie> IStudio.MovieWorks => [];

    IEnumerable<ISeries> IStudio.SeriesWorks => [];

    IEnumerable<IMetadata> IStudio.Works => [];

    #endregion
}
