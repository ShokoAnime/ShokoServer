using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace JmmConversionTool.Xml
{
    [XmlRoot(ElementName = "add")]
    public class Add
    {
        [XmlAttribute(AttributeName = "key")]
        public string Key { get; set; }
        [XmlAttribute(AttributeName = "value")]
        public string Value { get; set; }
    }

    [XmlRoot(ElementName = "appSettings")]
    public class AppSettings
    {
        [XmlElement(ElementName = "add")]
        public List<Add> Settings { get; set; }
    }
    [XmlRoot(ElementName = "configuration")]
    public class Configuration
    {
        [XmlElement(ElementName = "appSettings")]
        public AppSettings AppSettings { get; set; }
    }
}
