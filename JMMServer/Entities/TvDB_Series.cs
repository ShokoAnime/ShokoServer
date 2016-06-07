using System;
using System.Xml;
using JMMContracts;
using NLog;

namespace JMMServer.Entities
{
    public class TvDB_Series
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public TvDB_Series()
        {
            SeriesID = 0;
            Overview = string.Empty;
            SeriesName = string.Empty;
            Status = string.Empty;
            Banner = string.Empty;
            Fanart = string.Empty;
            Lastupdated = string.Empty;
            Poster = string.Empty;
        }

        public int TvDB_SeriesID { get; private set; }
        public int SeriesID { get; set; }
        public string Overview { get; set; }
        public string SeriesName { get; set; }
        public string Status { get; set; }
        public string Banner { get; set; }
        public string Fanart { get; set; }
        public string Lastupdated { get; set; }
        public string Poster { get; set; }

        public Contract_TvDB_Series ToContract()
        {
            var contract = new Contract_TvDB_Series();
            contract.TvDB_SeriesID = TvDB_SeriesID;
            contract.SeriesID = SeriesID;
            contract.Overview = Overview;
            contract.SeriesName = SeriesName;
            contract.Status = Status;
            contract.Banner = Banner;
            contract.Fanart = Fanart;
            contract.Lastupdated = Lastupdated;
            contract.Poster = Poster;

            return contract;
        }

        public void PopulateFromSearch(XmlDocument doc)
        {
            SeriesID = int.Parse(TryGetProperty(doc, "seriesid"));
            SeriesName = TryGetProperty(doc, "SeriesName");
            Overview = TryGetProperty(doc, "Overview");
            Banner = TryGetProperty(doc, "banner");
        }

        public void PopulateFromSeriesInfo(XmlDocument doc)
        {
            SeriesID = int.Parse(TryGetProperty(doc, "id"));
            SeriesName = TryGetProperty(doc, "SeriesName");
            Overview = TryGetProperty(doc, "Overview");
            Banner = TryGetProperty(doc, "banner");

            Status = TryGetProperty(doc, "Status");
            Fanart = TryGetProperty(doc, "fanart");
            Lastupdated = TryGetProperty(doc, "lastupdated");
            Poster = TryGetProperty(doc, "poster");
        }

        protected string TryGetProperty(XmlDocument doc, string propertyName)
        {
            try
            {
                var prop = doc["Data"]["Series"][propertyName].InnerText.Trim();
                return prop;
            }
            catch (Exception ex)
            {
                logger.ErrorException("Erorr in TryGetProperty: " + ex, ex);
            }

            return "";
        }
    }
}