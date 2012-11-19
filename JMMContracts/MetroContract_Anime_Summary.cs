using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class MetroContract_Anime_Summary
	{
		public int AnimeID { get; set; }
		public int AnimeSeriesID { get; set; }
		public string AnimeName { get; set; }
		public int AirDateAsSeconds { get; set; }
		public int BeginYear { get; set; }
		public int EndYear { get; set; }
		public string PosterName { get; set; }

		public int ImageType { get; set; }
		public int ImageID { get; set; }
	}
}
