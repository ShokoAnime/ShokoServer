using System.Xml.Serialization;

namespace Shoko.Server.Providers.JMMAutoUpdates
{
    [XmlType(AnonymousType = true)]
    public class Update
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
            get { return JMMAutoUpdatesHelper.ConvertToAbsoluteVersion(version); }
        }
    }
}