using Newtonsoft.Json;

namespace Shoko.Commons.Plex
{
    public class TagHolder
    {
        [JsonProperty("tag")] public string Tag { get; set; }
    }
}