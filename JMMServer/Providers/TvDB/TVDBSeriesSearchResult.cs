using System.Xml;
using JMMContracts;

namespace JMMServer.Providers.TvDB
{
    public class TVDBSeriesSearchResult
    {
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

        public Contract_TVDBSeriesSearchResult ToContract()
        {
            Contract_TVDBSeriesSearchResult contract = new Contract_TVDBSeriesSearchResult();
            contract.Id = this.Id;
            contract.SeriesID = this.SeriesID;
            contract.Overview = this.Overview;
            contract.SeriesName = this.SeriesName;
            contract.Banner = this.Banner;
            contract.Language = this.Language;
            return contract;
        }
    }
}