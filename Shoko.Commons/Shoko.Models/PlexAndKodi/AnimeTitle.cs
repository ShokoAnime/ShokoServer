using System;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Shoko.Models.PlexAndKodi
{
    [Serializable]
    [DataContract]
    public class AnimeTitle
    {
        [DataMember(EmitDefaultValue = false, Order = 1)]
        [XmlElement("Type")]
        public string Type { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlElement("Language")]
        public string Language { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 3)]
        [XmlElement("Title")]
        public string Title { get; set; }
    }
}