using System;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Shoko.Models.PlexAndKodi
{
    [Serializable]
    [DataContract]
    public class Contract_ImageDetails
    {
        [XmlAttribute("ID")]
        [DataMember(EmitDefaultValue = false, Order = 1)]
        public int ImageID { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlAttribute("Type")]
        public int ImageType { get; set; }
    }
}