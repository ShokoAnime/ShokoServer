using System;
using System.Xml.Serialization;
using AniDBAPI;
using JMMServer.Entities;

namespace JMMServer.WebCache
{
    [Serializable]
    [XmlRoot("AddCrossRef_AniDB_MAL_Request")]
    public class AddCrossRef_AniDB_MALRequest : XMLBase
    {
        protected int animeID;

        protected int mALID;

        protected string mALTitle = "";

        protected int startEpisodeNumber;

        protected int startEpisodeType;
        protected string username = "";


        // default constructor
        public AddCrossRef_AniDB_MALRequest()
        {
        }

        // default constructor
        public AddCrossRef_AniDB_MALRequest(CrossRef_AniDB_MAL data)
        {
            AnimeID = data.AnimeID;
            MALID = data.MALID;
            StartEpisodeType = data.StartEpisodeType;
            StartEpisodeNumber = data.StartEpisodeNumber;

            var username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            Username = username;
            MALTitle = data.MALTitle;
        }

        public string Username
        {
            get { return username; }
            set { username = value; }
        }

        public string MALTitle
        {
            get { return mALTitle; }
            set { mALTitle = value; }
        }

        public int AnimeID
        {
            get { return animeID; }
            set { animeID = value; }
        }

        public int MALID
        {
            get { return mALID; }
            set { mALID = value; }
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