using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// Overview information.
/// </summary>
public class Overview
{
    /// <summary>
    /// The overview value.
    /// </summary>
    [Required]
    public string Value { get; init; }

    /// <summary>
    /// Language code. Alpha 2.
    /// </summary>
    [Required]
    public string Language { get; init; }

    /// <summary>
    /// Indicates this is the default overview for the entity.
    /// </summary>
    public bool Default { get; init; }

    /// <summary>
    /// Indicates this is the user preferred overview.
    /// </summary>
    /// <value></value>
    public bool Preferred { get; init; }

    /// <summary>
    /// Indicates the source where the overview is from.
    /// </summary>
    [Required]
    public string Source { get; init; }

    public Overview(TMDB_Overview overview, string mainDescription = null, TMDB_Overview preferredDescription = null)
    {
        Value = overview.Value;
        Language = overview.Language.GetString();
        Default = overview.Language == TitleLanguage.English && !string.IsNullOrEmpty(mainDescription) && string.Equals(overview.Value, mainDescription);
        Preferred = overview.Equals(preferredDescription);
        Source = "TMDB";
    }
}
