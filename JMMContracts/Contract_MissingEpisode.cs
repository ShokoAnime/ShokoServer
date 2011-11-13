using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_MissingEpisode
	{
		public int EpisodeID { get; set; }
		public int AnimeID { get; set; }
		public string AnimeTitle { get; set; }
		public int EpisodeNumber { get; set; }
		public int EpisodeType { get; set; }
		public string GroupFileSummary { get; set; }

		public Contract_AnimeSeries AnimeSeries { get; set; }
	}
}
