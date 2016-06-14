using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using JMMServer.ImageDownload;
using System.Xml;
using NLog;
using JMMContracts;

namespace JMMServer.Entities
{
	public class TvDB_ImageWideBanner
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public int TvDB_ImageWideBannerID { get; private set; }
		public int Id { get; set; }
		public int SeriesID { get; set; }
		public string BannerPath { get; set; }
		public string BannerType { get; set; }
		public string BannerType2 { get; set; }
		public string Language { get; set; }
		public int Enabled { get; set; }
		public int? SeasonNumber { get; set; }

		public Contract_TvDB_ImageWideBanner ToContract()
		{
			Contract_TvDB_ImageWideBanner contract = new Contract_TvDB_ImageWideBanner();
			contract.TvDB_ImageWideBannerID = this.TvDB_ImageWideBannerID;
			contract.Id = this.Id;
			contract.SeriesID = this.SeriesID;
			contract.BannerPath = this.BannerPath;
			contract.BannerType = this.BannerType;
			contract.BannerType2 = this.BannerType2;
			contract.Language = this.Language;
			contract.Enabled = this.Enabled;
			contract.SeasonNumber = this.SeasonNumber;

			return contract;
		}

		public string FullImagePath
		{
			get
			{
				if (string.IsNullOrEmpty(BannerPath)) return "";

				string fname = BannerPath;
				fname = BannerPath.Replace("/", @"\");
				return Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
			}
		}

		public bool Populate(int seriesID, XmlNode node, TvDBImageNodeType nodeType)
		{
			try
			{
				this.SeriesID = seriesID;

				if (nodeType == TvDBImageNodeType.Series)
					SeasonNumber = null;
				else
					SeasonNumber = int.Parse(node["Season"].InnerText);

				Id = int.Parse(node["id"].InnerText);
				BannerPath = node["BannerPath"].InnerText;
				BannerType = node["BannerType"].InnerText;
				BannerType2 = node["BannerType2"].InnerText;
				Language = node["Language"].InnerText;

				return true;

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TvDB_ImageWideBanner.Populate: " + ex.ToString(), ex);
				return false;
			}
		}

	}
}
