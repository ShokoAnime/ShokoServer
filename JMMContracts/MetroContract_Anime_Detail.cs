using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class MetroContract_Anime_Detail
	{
		public int AnimeID { get; set; }
		public int AnimeSeriesID { get; set; }
		public string AnimeName { get; set; }
		public string AnimeType { get; set; }
		public int BeginYear { get; set; }
		public int EndYear { get; set; }
		public int PosterImageType { get; set; }
		public int PosterImageID { get; set; }
		public int FanartImageType { get; set; }
		public int FanartImageID { get; set; }
		public string Description { get; set; }
		public int EpisodeCountNormal { get; set; }
		public int EpisodeCountSpecial { get; set; }
		public int Rating { get; set; }
		public DateTime? AirDate { get; set; }
		public DateTime? EndDate { get; set; }
	}
}
