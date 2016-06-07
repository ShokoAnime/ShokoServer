using System;
using System.Xml.Serialization;
using AniDBAPI;

namespace JMMServer.WebCache
{
    [Serializable]
    [XmlRoot("DeleteCrossRef_AniDBTvDBAll_Request")]
    public class DeleteCrossRef_AniDBTvDBAll_Request : XMLBase
    {
        protected int seriesID;

        // default constructor
        public DeleteCrossRef_AniDBTvDBAll_Request()
        {
        }

        // default constructor
        public DeleteCrossRef_AniDBTvDBAll_Request(int tvDBID)
        {
            SeriesID = tvDBID;
        }

        public int SeriesID
        {
            get { return seriesID; }
            set { seriesID = value; }
        }
    }
}