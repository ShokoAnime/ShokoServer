using Newtonsoft.Json;

namespace Shoko.Commons.Plex.Login
{
    public class Location
    {
        [JsonProperty("code")] public string Code { get; set; }
        [JsonProperty("country")] public string Country { get; set; }
        [JsonProperty("city")] public string City { get; set; }
        [JsonProperty("subdivisions")] public string Subdivisions { get; set; }
        [JsonProperty("coordinates")] public string Coordinates { get; set; }
    }
}