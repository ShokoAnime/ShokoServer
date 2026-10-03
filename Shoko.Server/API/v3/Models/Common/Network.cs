using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;
using Shoko.Server.API.v3.Helpers;

namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// APIv3 Network Data Transfer Object (DTO).
/// </summary>
public class Network
{
    /// <summary>
    /// Network ID relative to the <see cref="Source"/>.
    /// </summary>
    [Required]
    public int ID { get; init; }

    /// <summary>
    /// The name of the studio.
    /// </summary>
    [Required]
    public string Name { get; init; }

    /// <summary>
    /// The country the studio originates from.
    /// </summary>
    [Required]
    public string CountryOfOrigin { get; init; }

    /// <summary>
    /// Entities produced by the studio in the local collection, both movies
    /// and/or shows.
    /// </summary>
    [Required]
    public int Size { get; init; }

    /// <summary>
    /// The source of which the studio metadata belongs to.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public DataSourceType Source { get; init; }

    public Network(INetwork company)
    {
        ID = TmdbCompatibility.TmdbID(company);
        Name = company.Name;
        CountryOfOrigin = company.CountryOfOrigin ?? string.Empty;
        Size = TmdbCompatibility.NetworkSize(company);
        Source = DataSourceType.TMDB;
    }
}
