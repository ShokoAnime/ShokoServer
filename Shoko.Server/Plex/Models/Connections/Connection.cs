using System.Diagnostics;
using System.Xml.Serialization;

namespace Shoko.Server.Plex.Models.Connections;

[XmlRoot(ElementName = "Connection")]
[DebuggerDisplay("Connection: Protocol = {Protocol}, Address = {Address}, Port = {Port}")]
public class Connection
{
    [XmlAttribute(AttributeName = "protocol")]
    public string Protocol { get; set; } = null!;

    [XmlAttribute(AttributeName = "address")]
    public string Address { get; set; } = null!;

    [XmlAttribute(AttributeName = "port")] public string Port { get; set; } = null!;
    [XmlAttribute(AttributeName = "uri")] public string Uri { get; set; } = null!;

    [XmlAttribute(AttributeName = "local")]
    public string Local { get; set; } = null!;
}
