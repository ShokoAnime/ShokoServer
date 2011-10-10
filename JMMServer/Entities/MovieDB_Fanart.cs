using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using JMMServer.Providers.MovieDB;
using System.IO;
using JMMServer.ImageDownload;
using JMMContracts;

namespace JMMServer.Entities
{
	public class MovieDB_Fanart
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public int MovieDB_FanartID { get; private set; }
		public string ImageID { get; set; }
		public int MovieId { get; set; }
		public string ImageType { get; set; }
		public string ImageSize { get; set; }
		public string URL { get; set; }
		public int ImageWidth { get; set; }
		public int ImageHeight { get; set; }
		public int Enabled { get; set; }

		public string FullImagePath
		{
			get
			{
				if (string.IsNullOrEmpty(URL)) return "";

				//strip out the base URL
				int pos = URL.IndexOf('/', 10);
				string fname = URL.Substring(pos + 1, URL.Length - pos - 1);
				fname = fname.Replace("/", @"\");
				return Path.Combine(ImageUtils.GetMovieDBImagePath(), fname);
			}
		}

		public void Populate(MovieDB_Image_Result searchResult, int movieID)
		{
			this.MovieId = movieID;
			this.ImageID = searchResult.ImageID;
			this.ImageType = searchResult.ImageType;
			this.ImageSize = searchResult.ImageSize;
			this.URL = searchResult.URL;
			this.ImageWidth = searchResult.ImageWidth;
			this.ImageHeight = searchResult.ImageHeight;
			this.Enabled = 1;
		}

		public Contract_MovieDB_Fanart ToContract()
		{
			Contract_MovieDB_Fanart contract = new Contract_MovieDB_Fanart();
			contract.MovieDB_FanartID = this.MovieDB_FanartID;
			contract.ImageID = this.ImageID;
			contract.MovieId = this.MovieId;
			contract.ImageType = this.ImageType;
			contract.ImageSize = this.ImageSize;
			contract.URL = this.URL;
			contract.ImageWidth = this.ImageWidth;
			contract.ImageHeight = this.ImageHeight;
			contract.Enabled = this.Enabled;
			return contract;
		}
	}
}
