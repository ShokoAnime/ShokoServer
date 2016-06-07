using System.Collections.Generic;
using System.Xml.Serialization;

namespace JMMServer.Providers.JMMAutoUpdates
{
    [XmlType(AnonymousType = true)]
    public class Updates
    {
        /// <remarks />
        [XmlArrayItem("update", IsNullable = false)]
        public List<Update> server { get; set; }

        /// <remarks />
        [XmlArrayItem("update", IsNullable = false)]
        public List<Update> desktop { get; set; }
    }
}