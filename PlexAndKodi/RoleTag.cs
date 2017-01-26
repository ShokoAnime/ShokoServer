using System;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Shoko.Models.PlexAndKodi
{
    [Serializable]
    [DataContract]
    public class RoleTag
    {
        [XmlAttribute("tag")]
        [DataMember(EmitDefaultValue = false, Order = 1)]
        public string Value { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlAttribute("role")]
        public string Role { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 3)]
        [XmlAttribute("roleDescription")]
        public string RoleDescription { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 4)]
        [XmlAttribute("rolePicture")]
        public string RolePicture { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 5)]
        [XmlAttribute("tagPicture")]
        public string TagPicture { get; set; }
    }
}