using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using System.Xml;
using JMMContracts;

namespace JMMServer.Entities
{
	public class TvDB_Series
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

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
			Contract_TvDB_Series contract = new Contract_TvDB_Series();
			contract.TvDB_SeriesID = this.TvDB_SeriesID;
			contract.SeriesID = this.SeriesID;
			contract.Overview = this.Overview;
			contract.SeriesName = this.SeriesName;
			contract.Status = this.Status;
			contract.Banner = this.Banner;
			contract.Fanart = this.Fanart;
			contract.Lastupdated = this.Lastupdated;
			contract.Poster = this.Poster;

			return contract;
		}

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

		public void PopulateFromSearch(XmlDocument doc)
		{
			this.SeriesID = int.Parse(TryGetProperty(doc, "seriesid"));
			this.SeriesName = TryGetProperty(doc, "SeriesName");
			this.Overview = TryGetProperty(doc, "Overview");
			this.Banner = TryGetProperty(doc, "banner");
		}

		public void PopulateFromSeriesInfo(XmlDocument doc)
		{
			this.SeriesID = int.Parse(TryGetProperty(doc, "id"));
			this.SeriesName = TryGetProperty(doc, "SeriesName");
			this.Overview = TryGetProperty(doc, "Overview");
			this.Banner = TryGetProperty(doc, "banner");

			this.Status = TryGetProperty(doc, "Status");
			this.Fanart = TryGetProperty(doc, "fanart");
			this.Lastupdated = TryGetProperty(doc, "lastupdated");
			this.Poster = TryGetProperty(doc, "poster");
		}

		protected string TryGetProperty(XmlDocument doc, string propertyName)
		{
			try
			{
				string prop = doc["Data"]["Series"][propertyName].InnerText.Trim();
				return prop;
			}
			catch (Exception ex)
			{
				logger.ErrorException("Erorr in TryGetProperty: " + ex.ToString(), ex);
			}

			return "";
		}
	}
}
