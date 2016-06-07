using System;
using System.Xml.Serialization;
using AniDBAPI;

namespace JMMServer.WebCache
{
    [Serializable]
    [XmlRoot("AniDB_Updated")]
    public class AniDB_UpdatedRequest : XMLBase
    {
        // default constructor
        public AniDB_UpdatedRequest()
        {
        }

        // default constructor
        public AniDB_UpdatedRequest(string uptime, string aidlist)
        {
            UpdatedTime = long.Parse(uptime);
            AnimeIDList = aidlist;

            var username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            Username = username;
        }

        public long UpdatedTime { get; set; }
        public string Username { get; set; }
        public string AnimeIDList { get; set; }
    }
}