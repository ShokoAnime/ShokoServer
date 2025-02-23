using System.Diagnostics;
using System.Xml.Serialization;

namespace Shoko.Models.Plex.Connections
{
    [XmlRoot(ElementName = "Connection")]
    [DebuggerDisplay("Connection: Protocol = {Protocol}, Address = {Address}, Port = {Port}")]
    public class Connection
    {
        [XmlAttribute(AttributeName = "protocol")]
        public string Protocol { get; set; }

        [XmlAttribute(AttributeName = "address")]
        public string Address { get; set; }

        [XmlAttribute(AttributeName = "port")] public string Port { get; set; }
        [XmlAttribute(AttributeName = "uri")] public string Uri { get; set; }

        [XmlAttribute(AttributeName = "local")]
        public string Local { get; set; }
    }
}