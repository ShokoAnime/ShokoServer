using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Shoko.Server.API.v1.Models;

[DataContract]
[Serializable]
[XmlType("Users")]
public class CL_PlexContract_Users
{
    [DataMember(EmitDefaultValue = false, Order = 1)]
    [XmlElement("User")]
    public List<CL_PlexContract_User> Users { get; set; }
    [DataMember(EmitDefaultValue = false, Order = 2)]
    [XmlAttribute("ErrorString")]
    public string ErrorString { get; set; }
}
