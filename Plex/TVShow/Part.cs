using Newtonsoft.Json;

namespace Shoko.Commons.Plex.TVShow
{
    public class Part
    {
        [JsonProperty("id")] public long Id { get; set; }
        [JsonProperty("key")] public string Key { get; set; }
        [JsonProperty("duration")] public long Duration { get; set; }
        [JsonProperty("file")] public string File { get; set; }
        [JsonProperty("size")] public long Size { get; set; }
        [JsonProperty("audioProfile")] public string AudioProfile { get; set; }
        [JsonProperty("container")] public string Container { get; set; }
        [JsonProperty("indexes")] public string Indexes { get; set; }
        [JsonProperty("videoProfile")] public string VideoProfile { get; set; }
        [JsonProperty("has64bitOffsets")] public bool? Has64BitOffsets { get; set; }
        [JsonProperty("optimizedForStreaming")] public bool? OptimizedForStreaming { get; set; }
        [JsonProperty("hasThumbnail")] public string HasThumbnail { get; set; }
    }
}