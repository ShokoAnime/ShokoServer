using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Nancy.Rest.Annotations.Atributes;

namespace Shoko.Models.PlexAndKodi
{
    [Serializable]
    [XmlType("Part")]
    [DataContract]
    public class Part
    {
        [DataMember(EmitDefaultValue = false, Order = 1)]
        [XmlAttribute("accessible")]
        public string Accessible { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlAttribute("exists")]
        public string Exists { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 3)]
        [XmlElement("Stream")]
        public List<Stream> Streams { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 4)]
        [XmlAttribute("size")]
        public string Size { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 5)]
        [XmlAttribute("duration")]
        public string Duration { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 6)]
        [XmlAttribute("key")]
        public string Key { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 7)]
        [XmlAttribute("local_key")]
        public string LocalKey { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 8)]
        [XmlAttribute("container")]
        public string Container { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 9)]
        [XmlAttribute("id")]
        public string Id { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 10)]
        [XmlAttribute("file")]
        public string File { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 11)]
        [XmlAttribute("optimizedForStreaming")]
        public string OptimizedForStreaming { get; set; }

        [Ignore]
        [XmlIgnore]
        public string Extension { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 12)]
        [XmlAttribute("has64bitOffsets")]
        public string Has64bitOffsets { get; set; }
    }
}