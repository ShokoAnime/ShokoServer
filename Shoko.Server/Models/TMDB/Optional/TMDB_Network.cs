using System.Collections.Generic;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_Network
{
    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_NetworkID { get; set; }

    /// <summary>
    /// TMDB Network ID.
    /// </summary>
    public int TmdbNetworkID { get; set; }

    /// <summary>
    /// Main name of the network on TMDB.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The country the network originates from.
    /// </summary>
    public string CountryOfOrigin { get; set; } = string.Empty;

    public IEnumerable<TMDB_Show_Network> NetworkXRefs { get; set; }

}
