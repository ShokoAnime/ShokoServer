using System.Runtime.Serialization;

namespace Shoko.Models.Plex.Libraries
{
    public class MediaContainer
    {
        [DataMember(Name = "size")] public long Size { get; set; }
        [DataMember(Name = "allowSync")] public bool AllowSync { get; set; }
        [DataMember(Name = "identifier")] public string Identifier { get; set; }
        [DataMember(Name = "mediaTagPrefix")] public string MediaTagPrefix { get; set; }
        [DataMember(Name = "mediaTagVersion")] public long MediaTagVersion { get; set; }
        [DataMember(Name = "title1")] public string Title1 { get; set; }
        [DataMember(Name = "Directory")] public Directory[] Directory { get; set; }
    }
}