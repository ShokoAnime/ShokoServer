using System;
using System.IO;
using System.Xml.Serialization;
using AniDBAPI;
using NLog;

namespace JMMServer.Providers.JMMAutoUpdates
{
    public class JMMAutoUpdatesHelper
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public static long ConvertToAbsoluteVersion(string version)
        {
            var numbers = version.Split('.');
            if (numbers.Length != 4) return 0;

            return int.Parse(numbers[3]) * 100 +
                   int.Parse(numbers[2]) * 100 * 100 +
                   int.Parse(numbers[1]) * 100 * 100 * 100 +
                   int.Parse(numbers[0]) * 100 * 100 * 100 * 100;
        }

        public static JMMVersions GetLatestVersionInfo()
        {
            try
            {
                // get the latest version as according to the release
                var uri = "http://www.jmediamanager.org/latestdownloads/versions.xml";
                var xml = APIUtils.DownloadWebPage(uri);

                var x = new XmlSerializer(typeof(JMMVersions));
                var myTest = (JMMVersions)x.Deserialize(new StringReader(xml));
                ServerState.Instance.ApplicationVersionLatest = myTest.versions.ServerVersionFriendly;

                return myTest;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }
    }
}