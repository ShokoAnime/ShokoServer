using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.Azure
{
    public class UserInfo
    {
        public int UserInfoId { get; set; }

        public string UsernameHash { get; set; }
        public string DatabaseType { get; set; }
        public long FileCount { get; set; }
        public string WindowsVersion { get; set; }
        public string JMMServerVersion { get; set; }
        public int TraktEnabled { get; set; }
        public int MALEnabled { get; set; }
        public int LocalUserCount { get; set; }
        public string CountryLocation { get; set; }
        public long LastEpisodeWatched { get; set; }
        public DateTime LastEpisodeWatchedAsDate { get; set; }
        public long DateTimeUpdatedUTC { get; set; }
        public DateTime DateTimeUpdated { get; set; }
        

        // optional JMM Desktop fields
        public string DashboardType { get; set; }
        public string VideoPlayer { get; set; }

        public override string ToString()
        {
            return string.Format("{0} - {1} - {2}", UsernameHash, JMMServerVersion, DatabaseType);
        }
    }
}
