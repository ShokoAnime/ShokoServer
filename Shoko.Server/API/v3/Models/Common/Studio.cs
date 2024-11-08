
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// APIv3 Studio Data Transfer Object (DTO).
/// </summary>
public class Studio
{
    /// <summary>
    /// Studio ID relative to the <see cref="Source"/>.
    /// </summary>
    public int ID { get; init; }

    /// <summary>
    /// The name of the studio.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// The country the studio originates from.
    /// </summary>
    public string CountryOfOrigin { get; init; }

    /// <summary>
    /// Entities produced by the studio in the local collection, both movies
    /// and/or shows.
    /// </summary>
    public int Size { get; init; }

    /// <summary>
    /// Logos used by the studio.
    /// </summary>
    public IReadOnlyList<Image> Logos { get; init; }

    /// <summary>
    /// The source of which the studio metadata belongs to.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public DataSource Source { get; init; }

    public Studio(TMDB_Company company)
    {
        ID = company.TmdbCompanyID;
        Name = company.Name;
        CountryOfOrigin = company.CountryOfOrigin;
        Size = company.GetTmdbCompanyCrossReferences().Count;
        Logos = company.GetImages(ImageEntityType.Logo)
            .Select(image => new Image(image))
            .ToList();
        Source = DataSource.TMDB;
    }
}
