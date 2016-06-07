using System.Xml.Serialization;

namespace JMMServer.Providers.JMMAutoUpdates
{
    [XmlType(AnonymousType = true)]
    public class Update
    {
        /// <remarks />
        public string version { get; set; }

        /// <remarks />
        public string change { get; set; }

        public long VersionAbs
        {
            get { return JMMAutoUpdatesHelper.ConvertToAbsoluteVersion(version); }
        }

        public override string ToString()
        {
            return string.Format("{0} - {1}", version, change);
        }
    }
}