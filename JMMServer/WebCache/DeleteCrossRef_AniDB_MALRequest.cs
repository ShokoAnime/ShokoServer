using System;
using System.Xml.Serialization;
using AniDBAPI;

namespace JMMServer.WebCache
{
    [Serializable]
    [XmlRoot("DeleteCrossRef_AniDB_MALRequest")]
    public class DeleteCrossRef_AniDB_MALRequest : XMLBase
    {
        protected string username = "";

        public string Username
        {
            get { return username; }
            set { username = value; }
        }

        protected int animeID = 0;

        public int AnimeID
        {
            get { return animeID; }
            set { animeID = value; }
        }

        protected int startEpisodeType = 0;

        public int StartEpisodeType
        {
            get { return startEpisodeType; }
            set { startEpisodeType = value; }
        }

        protected int startEpisodeNumber = 0;

        public int StartEpisodeNumber
        {
            get { return startEpisodeNumber; }
            set { startEpisodeNumber = value; }
        }

        // default constructor
        public DeleteCrossRef_AniDB_MALRequest()
        {
        }

        // default constructor
        public DeleteCrossRef_AniDB_MALRequest(int animeID, int epType, int epNumber)
        {
            this.AnimeID = animeID;
            this.StartEpisodeType = epType;
            this.StartEpisodeNumber = epNumber;

            string username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            this.Username = username;
        }
    }
}