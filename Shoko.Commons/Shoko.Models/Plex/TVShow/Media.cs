using System.Runtime.Serialization;

namespace Shoko.Models.Plex.TVShow
{
    public class Media
    {
        [DataMember(Name = "videoResolution")] public string VideoResolution { get; set; }
        [DataMember(Name = "id")] public long Id { get; set; }
        [DataMember(Name = "duration")] public long Duration { get; set; }
        [DataMember(Name = "bitrate")] public long Bitrate { get; set; }
        [DataMember(Name = "width")] public long Width { get; set; }
        [DataMember(Name = "height")] public long Height { get; set; }
        [DataMember(Name = "aspectRatio")] public double AspectRatio { get; set; }
        [DataMember(Name = "audioChannels")] public long AudioChannels { get; set; }
        [DataMember(Name = "audioCodec")] public string AudioCodec { get; set; }
        [DataMember(Name = "videoCodec")] public string VideoCodec { get; set; }
        [DataMember(Name = "container")] public string Container { get; set; }
        [DataMember(Name = "videoFrameRate")] public string VideoFrameRate { get; set; }
        [DataMember(Name = "audioProfile")] public string AudioProfile { get; set; }
        [DataMember(Name = "videoProfile")] public string VideoProfile { get; set; }
        [DataMember(Name = "Part")] public Part[] Part { get; set; }
        [DataMember(Name = "optimizedForStreaming")] public long? OptimizedForStreaming { get; set; }
        [DataMember(Name = "has64bitOffsets")] public bool? Has64BitOffsets { get; set; }
    }
}