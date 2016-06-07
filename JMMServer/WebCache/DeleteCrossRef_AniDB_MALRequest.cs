using System;
using System.Xml.Serialization;
using AniDBAPI;

namespace JMMServer.WebCache
{
    [Serializable]
    [XmlRoot("DeleteCrossRef_AniDB_MALRequest")]
    public class DeleteCrossRef_AniDB_MALRequest : XMLBase
    {
        protected int animeID;

        protected int startEpisodeNumber;

        protected int startEpisodeType;
        protected string username = "";

        // default constructor
        public DeleteCrossRef_AniDB_MALRequest()
        {
        }

        // default constructor
        public DeleteCrossRef_AniDB_MALRequest(int animeID, int epType, int epNumber)
        {
            AnimeID = animeID;
            StartEpisodeType = epType;
            StartEpisodeNumber = epNumber;

            var username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            Username = username;
        }

        public string Username
        {
            get { return username; }
            set { username = value; }
        }

        public int AnimeID
        {
            get { return animeID; }
            set { animeID = value; }
        }

        public int StartEpisodeType
        {
            get { return startEpisodeType; }
            set { startEpisodeType = value; }
        }

        public int StartEpisodeNumber
        {
            get { return startEpisodeNumber; }
            set { startEpisodeNumber = value; }
        }
    }
}