using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;
using Shoko.Server.API.v3.Models.Common;

namespace Shoko.Server.API.v3.Models.Metadata;

/// <summary>
/// An entry of any metadata source, as the generic <c>Metadata/{source}</c>
/// routes send it. The typed entries add what their kind has, after these.
/// </summary>
public class MetadataEntry
{
    #region Identity

    /// <summary>
    /// The source's own ID for the entry.
    /// </summary>
    [Required, JsonProperty(Order = -20)]
    public string ID { get; set; } = string.Empty;

    /// <summary>
    /// The source the entry belongs to.
    /// </summary>
    [Required, JsonProperty(Order = -19)]
    public MetadataSource Source { get; set; } = null!;

    /// <summary>
    /// What kind of entry it is.
    /// </summary>
    [Required, JsonProperty(Order = -18)]
    public MetadataEntityType Type { get; set; } = null!;

    /// <summary>
    /// The entry's full identifier, e.g. <c>anilist://series/21</c>, as
    /// <c>Metadata/Entry</c> and the bulk bodies take it.
    /// </summary>
    [Required, JsonProperty(Order = -17)]
    public string Guid { get; set; } = string.Empty;

    /// <summary>
    /// The route that serves the entry, relative to <c>/api/v3/</c>, with a
    /// <c>/</c> in its ID sent as <c>%2F</c>.
    /// </summary>
    [Required, JsonProperty(Order = -16)]
    public string Path { get; set; } = string.Empty;

    #endregion

    #region Text

    /// <summary>
    /// The preferred title, or <c>null</c> for a kind of entry that has none.
    /// </summary>
    [JsonProperty(Order = -15)]
    public string? Title { get; set; }

    /// <summary>
    /// Every title of the entry, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore, Order = -14)]
    public IReadOnlyList<Title>? Titles { get; set; }

    /// <summary>
    /// The preferred overview, an empty string when there is none, or
    /// <c>null</c> for a kind of entry that has no overviews.
    /// </summary>
    [JsonProperty(Order = -13)]
    public string? Overview { get; set; }

    /// <summary>
    /// Every overview of the entry, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore, Order = -12)]
    public IReadOnlyList<Overview>? Overviews { get; set; }

    #endregion

    #region Images

    /// <summary>
    /// Every image of the entry, grouped by type, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore, Order = -11)]
    public Images? Images { get; set; }

    #endregion
}

/// <summary>
/// The extra details a generic entry can be sent with. Each kind of entry
/// takes the details it has and ignores the rest.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum MetadataIncludeDetails
{
    /// <summary>
    /// Every title.
    /// </summary>
    Titles,

    /// <summary>
    /// Every overview.
    /// </summary>
    Overviews,

    /// <summary>
    /// Every image, grouped by type.
    /// </summary>
    Images,

    /// <summary>
    /// The tags, genres included, of a series or movie.
    /// </summary>
    Tags,

    /// <summary>
    /// The studios of a series or movie.
    /// </summary>
    Studios,

    /// <summary>
    /// The networks of a series.
    /// </summary>
    Networks,

    /// <summary>
    /// The cast of a series, season, episode or movie.
    /// </summary>
    Cast,

    /// <summary>
    /// The crew of a series, season, episode or movie.
    /// </summary>
    Crew,

    /// <summary>
    /// The links from AniDB to a series, episode or movie.
    /// </summary>
    CrossReferences,

    /// <summary>
    /// The external resources of a series, episode or movie.
    /// </summary>
    Resources,

    /// <summary>
    /// The content ratings of a series or movie.
    /// </summary>
    ContentRatings,

    /// <summary>
    /// The yearly seasons a series, season or movie aired in.
    /// </summary>
    YearlySeasons,
}
