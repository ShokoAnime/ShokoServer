using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Shoko.Models.PlexAndKodi
{
    [XmlType("Extras")]
    [Serializable]
    [DataContract]
    public class Extras
    {
        [DataMember(EmitDefaultValue = false, Order = 1)]
        [XmlAttribute("size")]
        public string Size { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlElement("Video")]
        public List<Video> Videos { get; set; }
    }
}