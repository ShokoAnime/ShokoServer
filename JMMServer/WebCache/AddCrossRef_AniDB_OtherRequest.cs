using System;
using System.Xml.Serialization;
using AniDBAPI;
using JMMServer.Entities;

namespace JMMServer.WebCache
{
    [Serializable]
    [XmlRoot("AddCrossRef_AniDB_Other_Request")]
    public class AddCrossRef_AniDB_OtherRequest : XMLBase
    {
        protected int animeID;

        protected string crossRefID = "";

        protected int crossRefType;
        protected string username = "";

        // default constructor
        public AddCrossRef_AniDB_OtherRequest()
        {
        }

        // default constructor
        public AddCrossRef_AniDB_OtherRequest(CrossRef_AniDB_Other data)
        {
            AnimeID = data.AnimeID;
            CrossRefID = data.CrossRefID;
            CrossRefType = data.CrossRefType;

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

        public string CrossRefID
        {
            get { return crossRefID; }
            set { crossRefID = value; }
        }

        public int CrossRefType
        {
            get { return crossRefType; }
            set { crossRefType = value; }
        }
    }
}