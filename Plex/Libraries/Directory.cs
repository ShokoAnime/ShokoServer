using System.Diagnostics;
using Newtonsoft.Json;

namespace Shoko.Commons.Plex.Libraries
{
    [DebuggerDisplay("Directory, Title = {Title}, Agent = {Agent}, Scanner = {Scanner}")]
    //[JsonConverter(typeof(PlexConverter))]
    public class Directory
    {
        [JsonProperty("allowSync")] public bool AllowSync { get; set; }
        [JsonProperty("art")] public string Art { get; set; }
        [JsonProperty("composite")] public string Composite { get; set; }
        [JsonProperty("filters")] public bool Filters { get; set; }
        [JsonProperty("refreshing")] public bool Refreshing { get; set; }
        [JsonProperty("thumb")] public string Thumb { get; set; }
        [JsonProperty("key")] public int Key { get; set; }
        [JsonProperty("type")] public PlexType Type { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("agent")] public string Agent { get; set; }
        [JsonProperty("scanner")] public string Scanner { get; set; }
        [JsonProperty("language")] public string Language { get; set; }
        [JsonProperty("uuid")] public string Uuid { get; set; }
        [JsonProperty("updatedAt")] public long UpdatedAt { get; set; }
        [JsonProperty("createdAt")] public long CreatedAt { get; set; }
        [JsonProperty("Location")] public Location[] Location { get; set; }
    }
}