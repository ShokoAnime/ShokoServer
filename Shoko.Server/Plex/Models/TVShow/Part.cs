using System.Runtime.Serialization;

namespace Shoko.Server.Plex.Models.TVShow;

public class Part
{
    [DataMember(Name = "id")] public long Id { get; set; }
    [DataMember(Name = "key")] public string Key { get; set; } = null!;
    [DataMember(Name = "duration")] public long Duration { get; set; }
    [DataMember(Name = "file")] public string File { get; set; } = null!;
    [DataMember(Name = "size")] public long Size { get; set; }
    [DataMember(Name = "audioProfile")] public string AudioProfile { get; set; } = null!;
    [DataMember(Name = "container")] public string Container { get; set; } = null!;
    [DataMember(Name = "indexes")] public string Indexes { get; set; } = null!;
    [DataMember(Name = "videoProfile")] public string VideoProfile { get; set; } = null!;
    [DataMember(Name = "has64bitOffsets")] public bool? Has64BitOffsets { get; set; }
    [DataMember(Name = "optimizedForStreaming")] public bool? OptimizedForStreaming { get; set; }
    [DataMember(Name = "hasThumbnail")] public string HasThumbnail { get; set; } = null!;
}
