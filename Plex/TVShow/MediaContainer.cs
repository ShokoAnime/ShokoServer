using Newtonsoft.Json;

namespace Shoko.Commons.Plex.TVShow
{
    public class MediaContainer
    {
        [JsonProperty("size")] public long Size { get; set; }
        [JsonProperty("allowSync")] public bool AllowSync { get; set; }
        [JsonProperty("art")] public string Art { get; set; }
        [JsonProperty("banner")] public string Banner { get; set; }
        [JsonProperty("identifier")] public string Identifier { get; set; }
        [JsonProperty("key")] public string Key { get; set; }
        [JsonProperty("librarySectionID")] public long LibrarySectionId { get; set; }
        [JsonProperty("librarySectionTitle")] public string LibrarySectionTitle { get; set; }
        [JsonProperty("librarySectionUUID")] public string LibrarySectionUuid { get; set; }
        [JsonProperty("mediaTagPrefix")] public string MediaTagPrefix { get; set; }
        [JsonProperty("mediaTagVersion")] public long MediaTagVersion { get; set; }
        [JsonProperty("mixedParents")] public bool MixedParents { get; set; }
        [JsonProperty("nocache")] public bool Nocache { get; set; }
        [JsonProperty("parentIndex")] public long ParentIndex { get; set; }
        [JsonProperty("parentTitle")] public string ParentTitle { get; set; }
        [JsonProperty("parentYear")] public long ParentYear { get; set; }
        [JsonProperty("theme")] public string Theme { get; set; }
        [JsonProperty("title1")] public string Title1 { get; set; }
        [JsonProperty("title2")] public string Title2 { get; set; }
        [JsonProperty("viewGroup")] public string ViewGroup { get; set; }
        [JsonProperty("viewMode")] public long ViewMode { get; set; }
        [JsonProperty("Metadata")] public Episode[] Metadata { get; set; }
    }
}