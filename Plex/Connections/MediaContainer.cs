using System.Collections.Generic;
using System.Xml.Serialization;

namespace Shoko.Commons.Plex.Connections
    {
        [XmlRoot(ElementName = "MediaContainer")]
        public class MediaContainer
        {
            [XmlElement(ElementName = "Device")] public List<MediaDevice> Device { get; set; }
            [XmlAttribute(AttributeName = "size")] public string Size { get; set; }
        }
    }