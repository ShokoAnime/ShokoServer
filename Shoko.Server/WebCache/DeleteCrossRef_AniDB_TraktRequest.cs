using System;
using System.Xml.Serialization;
using AniDBAPI;

namespace Shoko.Server.WebCache
{
    [Serializable]
    [XmlRoot("DeleteCrossRef_AniDB_TraktRequest")]
    public class DeleteCrossRef_AniDB_TraktRequest : XMLBase
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

        // default constructor
        public DeleteCrossRef_AniDB_TraktRequest()
        {
        }

        // default constructor
        public DeleteCrossRef_AniDB_TraktRequest(int animeID)
        {
            this.AnimeID = animeID;

            string username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            this.Username = username;
        }
    }
}