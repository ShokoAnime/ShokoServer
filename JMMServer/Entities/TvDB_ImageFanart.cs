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
	public class TvDB_ImageFanart
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public int TvDB_ImageFanartID { get; private set; }
		public int Id { get; set; }
		public int SeriesID { get; set; }
		public string BannerPath { get; set; }
		public string BannerType { get; set; }
		public string BannerType2 { get; set; }
		public string Colors { get; set; }
		public string Language { get; set; }
		public string ThumbnailPath { get; set; }
		public string VignettePath { get; set; }
		public int Enabled { get; set; }
		public int Chosen { get; set; }

		public Contract_TvDB_ImageFanart ToContract()
		{
			Contract_TvDB_ImageFanart contract = new Contract_TvDB_ImageFanart();
			contract.TvDB_ImageFanartID = this.TvDB_ImageFanartID;
			contract.Id = this.Id;
			contract.SeriesID = this.SeriesID;
			contract.BannerPath = this.BannerPath;
			contract.BannerType = this.BannerType;
			contract.BannerType2 = this.BannerType2;
			contract.Colors = this.Colors;
			contract.Language = this.Language;
			contract.ThumbnailPath = this.ThumbnailPath;
			contract.VignettePath = this.VignettePath;
			contract.Enabled = this.Enabled;
			contract.Chosen = this.Chosen;

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

		public string FullThumbnailPath
		{
			get
			{
				string fname = ThumbnailPath;
				fname = ThumbnailPath.Replace("/", @"\");
				return Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
			}
		}

		public bool Populate(int seriesID, XmlNode node)
		{
			try
			{
				this.SeriesID = seriesID;
				Id = int.Parse(node["id"].InnerText);
				BannerPath = node["BannerPath"].InnerText;
				BannerType = node["BannerType"].InnerText;
				BannerType2 = node["BannerType2"].InnerText;
				Colors = node["Colors"].InnerText;
				Language = node["Language"].InnerText;
				ThumbnailPath = node["ThumbnailPath"].InnerText;
				VignettePath = node["VignettePath"].InnerText;
				return true;

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TvDB_ImageFanart.Init: " + ex.ToString(), ex);
				return false;
			}
		}
	}
}
