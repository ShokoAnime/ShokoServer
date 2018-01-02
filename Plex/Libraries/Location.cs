using Newtonsoft.Json;

namespace Shoko.Commons.Plex.Libraries
{
    public class Location
    {
        [JsonProperty("id")] public long Id { get; set; }
        [JsonProperty("path")] public string Path { get; set; }
    }
}