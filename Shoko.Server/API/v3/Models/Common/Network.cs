using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// APIv3 Network Data Transfer Object (DTO).
/// </summary>
public class Network
{
    /// <summary>
    /// Network ID relative to the <see cref="Source"/>.
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
    /// The source of which the studio metadata belongs to.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public DataSource Source;

    public Network(TMDB_Network company)
    {
        ID = company.TmdbNetworkID;
        Name = company.Name;
        CountryOfOrigin = company.CountryOfOrigin;
        Size = company.NetworkXRefs.Count();
        Source = DataSource.TMDB;
    }
}
