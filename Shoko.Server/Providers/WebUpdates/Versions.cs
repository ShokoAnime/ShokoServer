namespace Shoko.Server.Providers.WebUpdates
{
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class Versions
    {
        /// <remarks/>
        public string serverversion { get; set; }

        /// <remarks/>
        public string desktopversion { get; set; }

        public override string ToString()
        {
            return string.Format("Server: {0} --- Desktop: {1}", serverversion, desktopversion);
        }

        public long ServerVersionAbs
        {
            get { return AutoUpdatesHelper.ConvertToAbsoluteVersion(serverversion); }
        }

        public string ServerVersionFriendly
        {
            get { return serverversion; }
        }

        public long DesktopVersionAbs
        {
            get { return AutoUpdatesHelper.ConvertToAbsoluteVersion(desktopversion); }
        }
    }
}