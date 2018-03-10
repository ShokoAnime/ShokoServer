using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Shoko.Models.PlexAndKodi
{
    [XmlType("Media")]
    [DataContract]
    [Serializable]
    public class Media : ICloneable
    {
        [DataMember(EmitDefaultValue = false, Order = 1)]
        [XmlElement("Part")]
        public List<Part> Parts { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 2)]
        [XmlAttribute("duration")]
        public long Duration { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 3)]
        [XmlAttribute("videoFrameRate")]
        public string VideoFrameRate { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 4)]
        [XmlAttribute("container")]
        public string Container { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 5)]
        [XmlAttribute("videoCodec")]
        public string VideoCodec { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 6)]
        [XmlAttribute("audioCodec")]
        public string AudioCodec { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 7)]
        [XmlAttribute("audioChannels")]
        public byte AudioChannels { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 8)]
        [XmlAttribute("aspectRatio")]
        public float AspectRatio { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 9)]
        [XmlAttribute("height")]
        public int Height { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 10)]
        [XmlAttribute("width")]
        public int Width { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 11)]
        [XmlAttribute("bitrate")]
        public int Bitrate { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 12)]
        [XmlAttribute("id")]
        public int Id { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 13)]
        [XmlAttribute("videoResolution")]
        public string VideoResolution { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 14)]
        [XmlAttribute("optimizedForStreaming")]
        public byte OptimizedForStreaming { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 15)]
        [XmlAttribute("chaptered")]
        public bool Chaptered { get; set; }

        public object Clone()
        {
            Media newMedia = new Media
            {
                Parts = Parts?.Select(a => (Part)a.Clone()).ToList(),
                Duration = Duration,
                VideoFrameRate = VideoFrameRate,
                Container = Container,
                VideoCodec = VideoCodec,
                AudioCodec = AudioCodec,
                AudioChannels = AudioChannels,
                AspectRatio = AspectRatio,
                Height = Height,
                Width = Width,
                Bitrate = Bitrate,
                Id = Id,
                VideoResolution = VideoResolution,
                OptimizedForStreaming = OptimizedForStreaming,
                Chaptered = Chaptered
            };
            return newMedia;
        }
    }
}