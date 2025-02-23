using System.Runtime.Serialization;

namespace Shoko.Models.Plex.TVShow
{
    public class Part
    {
        [DataMember(Name = "id")] public long Id { get; set; }
        [DataMember(Name = "key")] public string Key { get; set; }
        [DataMember(Name = "duration")] public long Duration { get; set; }
        [DataMember(Name = "file")] public string File { get; set; }
        [DataMember(Name = "size")] public long Size { get; set; }
        [DataMember(Name = "audioProfile")] public string AudioProfile { get; set; }
        [DataMember(Name = "container")] public string Container { get; set; }
        [DataMember(Name = "indexes")] public string Indexes { get; set; }
        [DataMember(Name = "videoProfile")] public string VideoProfile { get; set; }
        [DataMember(Name = "has64bitOffsets")] public bool? Has64BitOffsets { get; set; }
        [DataMember(Name = "optimizedForStreaming")] public bool? OptimizedForStreaming { get; set; }
        [DataMember(Name = "hasThumbnail")] public string HasThumbnail { get; set; }
    }
}