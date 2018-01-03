using System.Runtime.Serialization;

namespace Shoko.Models.Plex.TVShow
{
    public class MediaContainer
    {
        [DataMember(Name = "size")] public long Size { get; set; }
        [DataMember(Name = "allowSync")] public bool AllowSync { get; set; }
        [DataMember(Name = "art")] public string Art { get; set; }
        [DataMember(Name = "banner")] public string Banner { get; set; }
        [DataMember(Name = "identifier")] public string Identifier { get; set; }
        [DataMember(Name = "key")] public string Key { get; set; }
        [DataMember(Name = "librarySectionID")] public long LibrarySectionId { get; set; }
        [DataMember(Name = "librarySectionTitle")] public string LibrarySectionTitle { get; set; }
        [DataMember(Name = "librarySectionUUID")] public string LibrarySectionUuid { get; set; }
        [DataMember(Name = "mediaTagPrefix")] public string MediaTagPrefix { get; set; }
        [DataMember(Name = "mediaTagVersion")] public long MediaTagVersion { get; set; }
        [DataMember(Name = "mixedParents")] public bool MixedParents { get; set; }
        [DataMember(Name = "nocache")] public bool Nocache { get; set; }
        [DataMember(Name = "parentIndex")] public long ParentIndex { get; set; }
        [DataMember(Name = "parentTitle")] public string ParentTitle { get; set; }
        [DataMember(Name = "parentYear")] public long ParentYear { get; set; }
        [DataMember(Name = "theme")] public string Theme { get; set; }
        [DataMember(Name = "title1")] public string Title1 { get; set; }
        [DataMember(Name = "title2")] public string Title2 { get; set; }
        [DataMember(Name = "viewGroup")] public string ViewGroup { get; set; }
        [DataMember(Name = "viewMode")] public long ViewMode { get; set; }
        [DataMember(Name = "Metadata")] public Episode[] Metadata { get; set; }
    }
}