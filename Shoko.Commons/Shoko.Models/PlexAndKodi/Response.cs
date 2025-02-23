using System;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Shoko.Models.PlexAndKodi
{
    [Serializable]
    [DataContract]
    public class Response
    {
        [DataMember(EmitDefaultValue = false, Order = 1)]
        [XmlAttribute("Code")]
        public string Code { get; set; }

        [DataMember(EmitDefaultValue = false, Order = 2)]
        [XmlAttribute("Message")]
        public string Message { get; set; }
    }
}