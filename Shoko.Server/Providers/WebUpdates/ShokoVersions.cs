using System.Xml.Serialization;

namespace Shoko.Server.Providers.WebUpdates
{
    /// <remarks/>
    [XmlType(AnonymousType = true)]
    [XmlRoot("shokoversions", Namespace = "", IsNullable = false)]
    public partial class ShokoVersions
    {
        [XmlElement("versions")]
        public Versions versions { get; set; }

        [XmlElement("updates")]
        public Updates updates { get; set; }
    }
}