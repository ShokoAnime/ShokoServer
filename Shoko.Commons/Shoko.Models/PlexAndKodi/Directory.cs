using System;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Shoko.Models.PlexAndKodi
{
    [XmlRoot(ElementName = "Directory")]
    [Serializable]
    [DataContract]
    public class Directory : Video
    {
    }
}