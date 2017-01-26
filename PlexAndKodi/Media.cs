using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Shoko.Models.PlexAndKodi
{
    [XmlType("Media")]
    [DataContract]
    [Serializable]
    public class Media
    {
        [DataMember(EmitDefaultValue = false, Order = 1)]
        [XmlElement("Part")]
        public List<Part> Parts { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlAttribute("duration")]
        public string Duration { get; set; }

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
        public string AudioChannels { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 8)]
        [XmlAttribute("aspectRatio")]
        public string AspectRatio { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 9)]
        [XmlAttribute("height")]
        public string Height { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 10)]
        [XmlAttribute("width")]
        public string Width { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 11)]
        [XmlAttribute("bitrate")]
        public string Bitrate { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 12)]
        [XmlAttribute("id")]
        public string Id { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 13)]
        [XmlAttribute("videoResolution")]
        public string VideoResolution { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 14)]
        [XmlAttribute("optimizedForStreaming")]
        public string OptimizedForStreaming { get; set; }
    }
}