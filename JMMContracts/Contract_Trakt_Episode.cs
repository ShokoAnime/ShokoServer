using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_Trakt_Episode
	{
		public int Trakt_EpisodeID { get; set; }
		public int Trakt_ShowID { get; set; }
		public int Season { get; set; }
		public int EpisodeNumber { get; set; }
		public string Title { get; set; }
		public string URL { get; set; }
		public string Overview { get; set; }
		public string EpisodeImage { get; set; }
	}
}
