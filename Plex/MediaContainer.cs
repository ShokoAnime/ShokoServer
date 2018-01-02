using Newtonsoft.Json;

namespace Shoko.Commons.Plex
{
    public class MediaContainer<T>
    {
        [JsonProperty("MediaContainer")] public T Container { get; set; }
    }
}