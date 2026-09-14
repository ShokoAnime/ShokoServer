using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Anilist.Input;

/// <summary>
/// Body for looking up multiple AniList anime by ID, locally or remotely.
/// </summary>
public class AnilistBulkSearchBody
{
    /// <summary>
    /// The AniList anime IDs to look up.
    /// </summary>
    [Required]
    public List<int> IDs { get; set; } = [];
}
