﻿using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

# nullable enable
namespace Shoko.Server.API.v3.Models.Common;

public class ComponentVersion
{
    /// <summary>
    /// Version number.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Commit SHA.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Commit { get; set; }

    /// <summary>
    /// Release channel.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ReleaseChannel? ReleaseChannel { get; set; }

    /// <summary>
    /// Release date.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime? ReleaseDate { get; set; }

    /// <summary>
    /// Git Tag.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Tag { get; set; }

    /// <summary>
    /// A short description about this release/version.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }
}


[JsonConverter(typeof(StringEnumConverter))]
public enum ReleaseChannel
{
    Stable = 1,
    Dev = 2,
    Debug = 3,
}
