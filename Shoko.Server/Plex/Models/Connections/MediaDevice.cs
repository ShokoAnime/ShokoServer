using System.Collections.Generic;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Shoko.Server.Plex.Models.Connections;

[XmlRoot(ElementName = "Device")]
[DebuggerDisplay("Device: Name = {Name}, SourceTitle = {SourceTitle}, Device = {Device}")]
public class MediaDevice
{
    [XmlElement(ElementName = "Connection")]
    public List<Connection> Connection { get; set; } = null!;

    [XmlAttribute(AttributeName = "name")] public string Name { get; set; } = null!;

    [XmlAttribute(AttributeName = "product")]
    public string Product { get; set; } = null!;

    [XmlAttribute(AttributeName = "productVersion")]
    public string ProductVersion { get; set; } = null!;

    [XmlAttribute(AttributeName = "platform")]
    public string Platform { get; set; } = null!;

    [XmlAttribute(AttributeName = "platformVersion")]
    public string PlatformVersion { get; set; } = null!;

    [XmlAttribute(AttributeName = "device")]
    public string Device { get; set; } = null!;

    [XmlAttribute(AttributeName = "clientIdentifier")]
    public string ClientIdentifier { get; set; } = null!;

    [XmlAttribute(AttributeName = "createdAt")]
    public string CreatedAt { get; set; } = null!;

    [XmlAttribute(AttributeName = "lastSeenAt")]
    public string LastSeenAt { get; set; } = null!;

    [XmlAttribute(AttributeName = "provides")]
    public string Provides { get; set; } = null!;

    [XmlAttribute(AttributeName = "owned")]
    public string Owned { get; set; } = null!;

    [XmlAttribute(AttributeName = "accessToken")]
    public string AccessToken { get; set; } = null!;

    [XmlAttribute(AttributeName = "publicAddress")]
    public string PublicAddress { get; set; } = null!;

    [XmlAttribute(AttributeName = "httpsRequired")]
    public string HttpsRequired { get; set; } = null!;

    [XmlAttribute(AttributeName = "synced")]
    public string Synced { get; set; } = null!;

    [XmlAttribute(AttributeName = "relay")]
    public string Relay { get; set; } = null!;

    [XmlAttribute(AttributeName = "publicAddressMatches")]
    public string PublicAddressMatches { get; set; } = null!;

    [XmlAttribute(AttributeName = "presence")]
    public string Presence { get; set; } = null!;

    [XmlAttribute(AttributeName = "ownerId")]
    public string OwnerId { get; set; } = null!;

    [XmlAttribute(AttributeName = "home")] public string Home { get; set; } = null!;

    [XmlAttribute(AttributeName = "sourceTitle")]
    public string SourceTitle { get; set; } = null!;
}
