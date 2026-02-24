
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
