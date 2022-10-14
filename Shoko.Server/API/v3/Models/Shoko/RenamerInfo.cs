using Newtonsoft.Json;

namespace Shoko.Server.API.v3.Models.Shoko;

public class RenamerInfo
{
    public string Id { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
    public int Priority { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
    public string Description { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
    public bool Enabled { get; set; }
}
