using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace AniDBAPI
{
    public class XMLBase
    {
        public string ToXML()
        {
            var ns = new XmlSerializerNamespaces();
            ns.Add("", "");

            var serializer = new XmlSerializer(GetType());
            var settings = new XmlWriterSettings();
            settings.OmitXmlDeclaration = true; // Remove the <?xml version="1.0" encoding="utf-8"?>

            var sb = new StringBuilder();
            var writer = XmlWriter.Create(sb, settings);
            serializer.Serialize(writer, this, ns);

            return sb.ToString();
        }
    }
}