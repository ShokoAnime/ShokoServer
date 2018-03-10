using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Nancy.Rest.Annotations.Atributes;

namespace Shoko.Models.PlexAndKodi
{
    [Serializable]
    [XmlType("Part")]
    [DataContract]
    public class Part : ICloneable
    {
        [DataMember(EmitDefaultValue = true, Order = 1)]
        [XmlAttribute("accessible")]
        public byte Accessible { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 2)]
        [XmlAttribute("exists")]
        public byte Exists { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 3)]
        [XmlElement("Stream")]
        public List<Stream> Streams { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 4)]
        [XmlAttribute("size")]
        public long Size { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 5)]
        [XmlAttribute("duration")]
        public long Duration { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 6)]
        [XmlAttribute("key")]
        public string Key { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 7)]
        [XmlAttribute("local_key")]
        public string LocalKey { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 8)]
        [XmlAttribute("container")]
        public string Container { get; set; }

        [DataMember(EmitDefaultValue = true, Order = 9)]
        [XmlAttribute("id")]
        public int Id { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 10)]
        [XmlAttribute("file")]
        public string File { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 11)]
        [XmlAttribute("optimizedForStreaming")]
        public byte OptimizedForStreaming { get; set; }

        [Ignore]
        [XmlIgnore]
        public string Extension { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 12)]
        [XmlAttribute("has64bitOffsets")]
        public byte Has64bitOffsets { get; set; }

        public object Clone()
        {
            Part newPart = new Part
            {
                Accessible = Accessible,
                Exists = Exists,
                Size = Size,
                Duration = Duration,
                Key = Key,
                LocalKey = LocalKey,
                Container = Container,
                Id = Id,
                File = File,
                OptimizedForStreaming = OptimizedForStreaming,
                Extension = Extension,
                Has64bitOffsets = Has64bitOffsets,
                Streams = Streams?.Select(a => (Stream)a.Clone()).ToList()
            };
            return newPart;
        }
    }
}