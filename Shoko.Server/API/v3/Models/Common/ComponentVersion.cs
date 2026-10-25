using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Core;

namespace Shoko.Server.API.v3.Models.Common;

public class ComponentVersion
{
    /// <summary>
    /// Version number.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Minimum Shoko Server version compatible with the web UI release.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? MinimumServerVersion { get; set; }

    /// <summary>
    /// Maximum Shoko Server version compatible with the web UI release.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? MaximumServerVersion { get; set; }

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

    /// <summary>
    /// Indicates the web UI release ships with the server or its plugin.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsBundled { get; set; }

    /// <summary>
    /// Why the web UI release cannot be used, if it cannot.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? IncompatibleReason { get; set; }
}
