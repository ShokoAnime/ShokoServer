using System;
using System.Xml;
using NLog;
using Shoko.Commons.Utils;
using Shoko.Server.Server;

namespace Shoko.Server.Providers.JMMAutoUpdates
{
    public class JMMAutoUpdatesHelper
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();


        public static long ConvertToAbsoluteVersion(string version)
        {
            string[] numbers = version.Split('.');
            if (numbers.Length != 4) return 0;

            return int.Parse(numbers[3]) * 100 +
                   int.Parse(numbers[2]) * 100 * 100 +
                   int.Parse(numbers[1]) * 100 * 100 * 100 +
                   int.Parse(numbers[0]) * 100 * 100 * 100 * 100;
        }

        /*
        public static Providers.JMMAutoUpdates.JMMVersions GetLatestVersionInfo()
        {
            try
            {
                // get the latest version as according to the release
                string uri = string.Format("http://shokoanime.com/files/versions.xml");
                string xml = AniDBAPI.APIUtils.DownloadWebPage(uri);

                XmlSerializer x = new XmlSerializer(typeof(Providers.JMMAutoUpdates.JMMVersions));
                Providers.JMMAutoUpdates.JMMVersions myTest =
                    (Providers.JMMAutoUpdates.JMMVersions) x.Deserialize(new StringReader(xml));
                ServerState.Instance.ApplicationVersionLatest = myTest.versions.ServerVersionFriendly;

                return myTest;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }*/

        public static string GetLatestVersionNumber(string channel)
        {
            string versionNumber = string.Empty;
            try
            {
                // get the latest version as according to the release
                string uri = "http://shokoanime.com/files/versions.xml";
                string xml = Misc.DownloadWebPage(uri, null, true);

                XmlDocument xmldoc = new XmlDocument();
                xmldoc.LoadXml(xml);
                // Load something into xmldoc
                var nodeVersion = xmldoc.SelectSingleNode(
                    string.Format("//versioncheck/shokoserver/{0}/version", channel.ToLower()));
                versionNumber = nodeVersion.InnerText;
                ServerState.Instance.ApplicationVersionLatest = versionNumber;
            }
            catch (Exception ex)
            {
                logger.Error("Error during GetLatestVersionNumber: " + ex.Message);
            }

            return versionNumber;
        }
    }
}