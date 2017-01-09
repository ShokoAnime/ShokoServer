using System;
using System.Xml.Serialization;
using AniDBAPI;
using Shoko.Models.Server;

namespace Shoko.Server.WebCache
{
    [Serializable]
    [XmlRoot("AddCrossRef_AniDB_MAL_Request")]
    public class AddCrossRef_AniDB_MALRequest : XMLBase
    {
        protected string username = "";

        public string Username
        {
            get { return username; }
            set { username = value; }
        }

        protected string mALTitle = "";

        public string MALTitle
        {
            get { return mALTitle; }
            set { mALTitle = value; }
        }

        protected int animeID = 0;

        public int AnimeID
        {
            get { return animeID; }
            set { animeID = value; }
        }

        protected int mALID = 0;

        public int MALID
        {
            get { return mALID; }
            set { mALID = value; }
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
        public AddCrossRef_AniDB_MALRequest()
        {
        }

        // default constructor
        public AddCrossRef_AniDB_MALRequest(CrossRef_AniDB_MAL data)
        {
            this.AnimeID = data.AnimeID;
            this.MALID = data.MALID;
            this.StartEpisodeType = data.StartEpisodeType;
            this.StartEpisodeNumber = data.StartEpisodeNumber;

            string username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                username = Constants.AnonWebCacheUsername;

            this.Username = username;
            this.MALTitle = data.MALTitle;
        }
    }
}