using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.JMMAutoUpdates
{
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class Update
    {


        /// <remarks/>
        public string version { get; set; }

        /// <remarks/>
        public string change { get; set; }

        public override string ToString()
        {
            return string.Format("{0} - {1}", version, change);
        }

        public long VersionAbs
        {
            get
            {
                return JMMAutoUpdatesHelper.ConvertToAbsoluteVersion(version);
            }

        }
    }
}
