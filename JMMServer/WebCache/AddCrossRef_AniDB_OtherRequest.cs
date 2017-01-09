using System;
using System.Xml.Serialization;
using AniDBAPI;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.WebCache
{
    [Serializable]
    [XmlRoot("AddCrossRef_AniDB_Other_Request")]
    public class AddCrossRef_AniDB_OtherRequest : XMLBase
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

        protected string crossRefID = "";

        public string CrossRefID
        {
            get { return crossRefID; }
            set { crossRefID = value; }
        }

        protected int crossRefType = 0;

        public int CrossRefType
        {
            get { return crossRefType; }
            set { crossRefType = value; }
        }

        // default constructor
        public AddCrossRef_AniDB_OtherRequest()
        {
        }

        // default constructor
        public AddCrossRef_AniDB_OtherRequest(SVR_CrossRef_AniDB_Other data)
        {
            this.AnimeID = data.AnimeID;
            this.CrossRefID = data.CrossRefID;
            this.CrossRefType = data.CrossRefType;

            string username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            this.Username = username;
        }
    }
}