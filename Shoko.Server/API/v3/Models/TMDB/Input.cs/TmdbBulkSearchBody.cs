using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.TMDB.Input;

/// <summary>
/// APIv3 The Movie DataBase (TMDB) Bulk Search Data Transfer Object (DTO).
/// </summary>
public class TmdbBulkSearchBody
{
    /// <summary>
    /// The list of TMDB IDs to search for.
    /// </summary>
    /// <remarks>
    /// The list can be include duplicates, in which case you want the same
    /// metadata returned multiple times at different indexes, and the server
    /// will take care of the de-duplication before fetching it remotely from
    /// TMDB.
    /// </remarks>
    [Required, MinLength(1), MaxLength(25)]
    public List<int> IDs { get; set; } = [];
}
