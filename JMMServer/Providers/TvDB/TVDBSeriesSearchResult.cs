using System.Xml;
using JMMContracts;

namespace JMMServer.Providers.TvDB
{
    public class TVDBSeriesSearchResult
    {
        public TVDBSeriesSearchResult()
        {
            Id = string.Empty;
            SeriesID = 0;
            Overview = string.Empty;
            SeriesName = string.Empty;
            Banner = string.Empty;
        }

        public TVDBSeriesSearchResult(XmlNode series)
        {
            if (series["seriesid"] != null) SeriesID = int.Parse(series["seriesid"].InnerText);
            if (series["SeriesName"] != null) SeriesName = series["SeriesName"].InnerText;
            if (series["id"] != null) Id = series["id"].InnerText;
            if (series["Overview"] != null) Overview = series["Overview"].InnerText;
            if (series["banner"] != null) Banner = series["banner"].InnerText;
            if (series["language"] != null) Language = series["language"].InnerText;
        }

        public string Id { get; set; }
        public int SeriesID { get; set; }
        public string Overview { get; set; }
        public string SeriesName { get; set; }
        public string Banner { get; set; }
        public string Language { get; set; }

        public override string ToString()
        {
            return "TVDBSeriesSearchResult: " + Id + ":" + SeriesID + ":" + SeriesName + " - banner: " + Banner;
        }

        public Contract_TVDBSeriesSearchResult ToContract()
        {
            var contract = new Contract_TVDBSeriesSearchResult();
            contract.Id = Id;
            contract.SeriesID = SeriesID;
            contract.Overview = Overview;
            contract.SeriesName = SeriesName;
            contract.Banner = Banner;
            contract.Language = Language;
            return contract;
        }
    }
}