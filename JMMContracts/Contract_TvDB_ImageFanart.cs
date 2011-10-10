using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_TvDB_ImageFanart
	{
		public int TvDB_ImageFanartID { get; set; }
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
	}
}
