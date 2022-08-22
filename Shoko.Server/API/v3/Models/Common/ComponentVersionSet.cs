
# nullable enable
using Newtonsoft.Json;

namespace Shoko.Server.API.v3.Models.Common;

public class ComponentVersionSet
{
    /// <summary>
    /// Shoko.Server version.
    /// </summary>
    public ComponentVersion Server { get; set; } = new();

    /// <summary>
    /// Shoko.Commons version. Will be removed in the future when Commons is
    /// merged back into the server.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ComponentVersion? Commons { get; set; }

    /// <summary>
    /// Shoko.Models version. Will be removed in the future when Commons is
    /// merged back into the server.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ComponentVersion? Models { get; set; }

    /// <summary>
    /// MediaInfo version.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ComponentVersion? MediaInfo { get; set; }

    /// <summary>
    /// Web UI version, if an install is found.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ComponentVersion? WebUI { get; set; }
}
