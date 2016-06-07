using JMMContracts;

namespace JMMServer.WebCache
{
    public class AppVersionsResult
    {
        // default constructor

        public string JMMServerVersion { get; set; }
        public string JMMServerDownload { get; set; }

        public string JMMDesktopVersion { get; set; }
        public string JMMDesktopDownload { get; set; }

        public string MyAnime3Version { get; set; }
        public string MyAnime3Download { get; set; }

        public Contract_AppVersions ToContract()
        {
            var contract = new Contract_AppVersions();

            contract.JMMServerVersion = JMMServerVersion;
            contract.JMMServerDownload = JMMServerDownload;

            contract.JMMDesktopVersion = JMMDesktopVersion;
            contract.JMMDesktopDownload = JMMDesktopDownload;

            contract.MyAnime3Version = MyAnime3Version;
            contract.MyAnime3Download = MyAnime3Download;

            return contract;
        }
    }
}