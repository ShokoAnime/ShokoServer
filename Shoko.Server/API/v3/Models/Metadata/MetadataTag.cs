using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.API.v3.Models.Common;

namespace Shoko.Server.API.v3.Models.Metadata;

/// <summary>
/// A tag or genre of any metadata source.
/// </summary>
public class MetadataTag
{
    /// <summary>
    /// The source's own ID for the tag.
    /// </summary>
    [Required]
    public string ID { get; init; } = string.Empty;

    /// <summary>
    /// The source the tag belongs to.
    /// </summary>
    [Required]
    public MetadataSource Source { get; init; } = null!;

    /// <summary>
    /// The tag's name.
    /// </summary>
    [Required]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// What the tag means, or <c>null</c> when left out.
    /// </summary>
    public string? Overview { get; init; }

    /// <summary>
    /// Whether it is a tag, a genre or a keyword.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public TagKind Kind { get; init; }

    /// <summary>
    /// The category the source files the tag under, if any.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Whether the tag gives something away.
    /// </summary>
    [Required]
    public bool IsSpoiler { get; init; }

    /// <summary>
    /// Whether the tag marks adult content.
    /// </summary>
    [Required]
    public bool IsRestricted { get; init; }

    /// <summary>
    /// How strongly the tag applies to the entry it was read through, if the
    /// source says.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? Weight { get; init; }

    /// <summary>
    /// Whether the tag gives something away for the entry it was read
    /// through.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsLocalSpoiler { get; init; }

    /// <summary>
    /// How many entries have the tag, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? Size { get; init; }
}

/// <summary>
/// A studio of any metadata source.
/// </summary>
public class MetadataStudio
{
    /// <summary>
    /// The source's own ID for the studio.
    /// </summary>
    [Required]
    public string ID { get; init; } = string.Empty;

    /// <summary>
    /// The source the studio belongs to.
    /// </summary>
    [Required]
    public MetadataSource Source { get; init; } = null!;

    /// <summary>
    /// The studio's name.
    /// </summary>
    [Required]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The studio's own page on its source's site, or <c>null</c> when it has
    /// none.
    /// </summary>
    public string? SiteUrl { get; init; }

    /// <summary>
    /// The studio's name in its own language, if the source gives it.
    /// </summary>
    public string? OriginalName { get; init; }

    /// <summary>
    /// The country the studio originates from, usually an ISO 3166-1 code,
    /// or <c>null</c> when its source does not say.
    /// </summary>
    public string? CountryOfOrigin { get; init; }

    /// <summary>
    /// What the studio did.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public StudioType StudioType { get; init; }

    /// <summary>
    /// Whether the studio is a stub: only what a link named, kept until its
    /// source is asked for the rest.
    /// </summary>
    [Required]
    public bool IsStub { get; init; }

    /// <summary>
    /// How many series and movies the studio worked on.
    /// </summary>
    [Required]
    public int Size { get; init; }

    /// <summary>
    /// The studio's logos.
    /// </summary>
    [Required]
    public IReadOnlyList<Image> Logos { get; init; } = [];
}

/// <summary>
/// A network of any metadata source.
/// </summary>
public class MetadataNetwork
{
    /// <summary>
    /// The source's own ID for the network.
    /// </summary>
    [Required]
    public string ID { get; init; } = string.Empty;

    /// <summary>
    /// The source the network belongs to.
    /// </summary>
    [Required]
    public MetadataSource Source { get; init; } = null!;

    /// <summary>
    /// The network's name.
    /// </summary>
    [Required]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The country the network originates from, usually an ISO 3166-1 code,
    /// or <c>null</c> when its source does not say.
    /// </summary>
    public string? CountryOfOrigin { get; init; }

    /// <summary>
    /// The network's own page on its source's site, or <c>null</c> when it has
    /// none.
    /// </summary>
    public string? SiteUrl { get; init; }

    /// <summary>
    /// Whether the network is a stub: only what a link named, kept until its
    /// source is asked for the rest.
    /// </summary>
    [Required]
    public bool IsStub { get; init; }

    /// <summary>
    /// How many series aired on the network, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? Size { get; init; }

    /// <summary>
    /// The network's logos.
    /// </summary>
    [Required]
    public IReadOnlyList<Image> Logos { get; init; } = [];
}
