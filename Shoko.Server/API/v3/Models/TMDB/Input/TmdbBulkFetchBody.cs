using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Shoko.Plugin.Abstractions.DataModels;

#nullable enable
namespace Shoko.Server.API.v3.Models.TMDB.Input;

public class TmdbBulkFetchBody<TDetails>
    where TDetails : struct, System.Enum
{
    [Required]
    public List<int> IDs { get; set; } = [];

    public HashSet<TDetails>? Include { get; set; } = null;

    public HashSet<TitleLanguage>? Language { get; set; } = null;
}
