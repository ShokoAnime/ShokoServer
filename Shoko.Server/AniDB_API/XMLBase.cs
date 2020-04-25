using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace AniDBAPI
{
    public class XMLBase
    {
        public string ToXML()
        {
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", string.Empty);

            XmlSerializer serializer = new XmlSerializer(GetType());
            XmlWriterSettings settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true // Remove the <?xml version="1.0" encoding="utf-8"?>
            };
            StringBuilder sb = new StringBuilder();
            XmlWriter writer = XmlWriter.Create(sb, settings);
            serializer.Serialize(writer, this, ns);

            return sb.ToString();
        }
    }
}