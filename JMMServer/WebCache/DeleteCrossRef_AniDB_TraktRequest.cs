using System;
using System.Xml.Serialization;
using AniDBAPI;

namespace JMMServer.WebCache
{
    [Serializable]
    [XmlRoot("DeleteCrossRef_AniDB_TraktRequest")]
    public class DeleteCrossRef_AniDB_TraktRequest : XMLBase
    {
        protected int animeID;
        protected string username = "";

        // default constructor
        public DeleteCrossRef_AniDB_TraktRequest()
        {
        }

        // default constructor
        public DeleteCrossRef_AniDB_TraktRequest(int animeID)
        {
            AnimeID = animeID;

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
    }
}