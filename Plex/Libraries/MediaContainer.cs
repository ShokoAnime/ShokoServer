using Newtonsoft.Json;

namespace Shoko.Commons.Plex.Libraries
{
    public class MediaContainer
    {
        [JsonProperty("size")] public long Size { get; set; }
        [JsonProperty("allowSync")] public bool AllowSync { get; set; }
        [JsonProperty("identifier")] public string Identifier { get; set; }
        [JsonProperty("mediaTagPrefix")] public string MediaTagPrefix { get; set; }
        [JsonProperty("mediaTagVersion")] public long MediaTagVersion { get; set; }
        [JsonProperty("title1")] public string Title1 { get; set; }
        [JsonProperty("Directory")] public Directory[] Directory { get; set; }
    }
}