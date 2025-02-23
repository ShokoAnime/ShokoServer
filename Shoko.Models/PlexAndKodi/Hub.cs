using System;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Shoko.Models.PlexAndKodi
{
    [XmlType("Hub")]
    [Serializable]
    [DataContract]
    public class Hub
    {
        [DataMember(EmitDefaultValue = false, Order = 1)]
        [XmlAttribute("key")]
        public string Key { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlAttribute("type")]
        public string Type { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 3)]
        [XmlAttribute("hubIdentifier")]
        public string HubIdentifier { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 4)]
        [XmlAttribute("size")]
        public string Size { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 5)]
        [XmlAttribute("title")]
        public string Title { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 6)]
        [XmlAttribute("more")]
        public string More { get; set; }
    }
}