using System.Xml.Serialization;

namespace Shoko.Server.Providers.JMMAutoUpdates
{
    /// <remarks/>
    [XmlType(AnonymousType = true)]
    [XmlRoot("jmmversions", Namespace = "", IsNullable = false)]
    public partial class JMMVersions
    {
        [XmlElement("versions")]
        public Versions versions { get; set; }

        [XmlElement("updates")]
        public Updates updates { get; set; }
    }
}