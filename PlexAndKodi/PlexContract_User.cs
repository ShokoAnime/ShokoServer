using System;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Shoko.Models.PlexAndKodi
{
    [DataContract]
    [Serializable]
    [XmlType("User")]
    public class PlexContract_User
    {
        [XmlAttribute("id")]
        [DataMember(EmitDefaultValue = false, Order = 1)]
        public string id { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlAttribute("name")]
        public string name { get; set; }
    }
}