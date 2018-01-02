using Newtonsoft.Json;

namespace Shoko.Commons.Plex.Login
{
    public class Subscription
    {
        [JsonProperty("active")] public bool Active { get; set; }
        [JsonProperty("status")] public string Status { get; set; }
        [JsonProperty("plan")] public string Plan { get; set; }
        [JsonProperty("features")] public string[] Features { get; set; }
    }
}