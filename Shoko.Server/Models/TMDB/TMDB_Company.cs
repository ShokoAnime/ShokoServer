using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Shoko.Plugin.Abstractions.Enums;
using TMDbLib.Objects.General;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_Company
{
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

    public virtual ICollection<TMDB_Image_Company> ImageXRefs { get; set; }

    public virtual ICollection<TMDB_Company_Entity> XRefs { get; set; }

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

    [NotMapped]
    public IEnumerable<TMDB_Image> Images => ImageXRefs.Select(a => new
    {
        a.ImageType, Image = a.Image
    }).Where(a => a.Image != null).Select(a => new TMDB_Image
    {
        ImageType = a.ImageType,
        RemoteFileName = a.Image!.RemoteFileName,
        IsEnabled = a.Image.IsEnabled,
        IsPreferred = a.Image.IsPreferred,
        LanguageCode = a.Image.LanguageCode,
        Height = a.Image.Height,
        Width = a.Image.Width,
        TMDB_ImageID = a.Image.TMDB_ImageID,
        UserRating = a.Image.UserRating,
        UserVotes = a.Image.UserVotes
    });

    public IReadOnlyList<TMDB_Image> GetImages(ImageEntityType? entityType) => Images.Where(a => a.ImageType == entityType).ToList();
}
