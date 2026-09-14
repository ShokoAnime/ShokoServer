using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Anilist.Input;

/// <summary>
/// Body for fetching multiple local AniList entities at once.
/// </summary>
public class AnilistBulkFetchBody<TDetails>
    where TDetails : struct, Enum
{
    /// <summary>
    /// The AniList IDs to fetch.
    /// </summary>
    [Required]
    public List<int> IDs { get; set; } = [];

    /// <summary>
    /// Extra details to include.
    /// </summary>
    public HashSet<TDetails>? Include { get; set; } = null;
}
