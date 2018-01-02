using Newtonsoft.Json;

namespace Shoko.Commons.Plex.TVShow
{
    public class Media
    {
        [JsonProperty("videoResolution")] public string VideoResolution { get; set; }
        [JsonProperty("id")] public long Id { get; set; }
        [JsonProperty("duration")] public long Duration { get; set; }
        [JsonProperty("bitrate")] public long Bitrate { get; set; }
        [JsonProperty("width")] public long Width { get; set; }
        [JsonProperty("height")] public long Height { get; set; }
        [JsonProperty("aspectRatio")] public double AspectRatio { get; set; }
        [JsonProperty("audioChannels")] public long AudioChannels { get; set; }
        [JsonProperty("audioCodec")] public string AudioCodec { get; set; }
        [JsonProperty("videoCodec")] public string VideoCodec { get; set; }
        [JsonProperty("container")] public string Container { get; set; }
        [JsonProperty("videoFrameRate")] public string VideoFrameRate { get; set; }
        [JsonProperty("audioProfile")] public string AudioProfile { get; set; }
        [JsonProperty("videoProfile")] public string VideoProfile { get; set; }
        [JsonProperty("Part")] public Part[] Part { get; set; }
        [JsonProperty("optimizedForStreaming")] public long? OptimizedForStreaming { get; set; }
        [JsonProperty("has64bitOffsets")] public bool? Has64BitOffsets { get; set; }
    }
}