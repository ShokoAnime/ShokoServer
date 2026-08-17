using System.Runtime.Serialization;

namespace Shoko.Server.Plex.Models.Collection;

public class MediaContainer
{
    [DataMember(Name = "size")] public long Size { get; set; }
    [DataMember(Name = "allowSync")] public bool AllowSync { get; set; }
    [DataMember(Name = "art")] public string Art { get; set; } = null!;
    [DataMember(Name = "identifier")] public string Identifier { get; set; } = null!;
    [DataMember(Name = "librarySectionID")] public long LibrarySectionId { get; set; }
    [DataMember(Name = "librarySectionTitle")] public string LibrarySectionTitle { get; set; } = null!;
    [DataMember(Name = "librarySectionUUID")] public string LibrarySectionUuid { get; set; } = null!;
    [DataMember(Name = "mediaTagPrefix")] public string MediaTagPrefix { get; set; } = null!;
    [DataMember(Name = "mediaTagVersion")] public long MediaTagVersion { get; set; }
    [DataMember(Name = "nocache")] public bool Nocache { get; set; }
    [DataMember(Name = "thumb")] public string Thumb { get; set; } = null!;
    [DataMember(Name = "title1")] public string Title1 { get; set; } = null!;
    [DataMember(Name = "title2")] public string Title2 { get; set; } = null!;
    [DataMember(Name = "viewGroup")] public string ViewGroup { get; set; } = null!;
    [DataMember(Name = "Metadata")] public PlexLibrary[] Metadata { get; set; } = null!;
}
