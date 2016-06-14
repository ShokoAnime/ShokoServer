using System.Xml.Serialization;

namespace JMMServer.Providers.JMMAutoUpdates
{
    /// <remarks/>
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    [System.Xml.Serialization.XmlRootAttribute("jmmversions", Namespace = "", IsNullable = false)]
    public partial class JMMVersions
    {
        [XmlElement("versions")]
        public Versions versions { get; set; }

        [XmlElement("updates")]
        public Updates updates { get; set; }
    }
}