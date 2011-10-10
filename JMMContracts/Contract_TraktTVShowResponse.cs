using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_TraktTVShowResponse
	{
		public string title { get; set; }
		public string year { get; set; }
		public string url { get; set; }
		public string first_aired { get; set; }
		public string country { get; set; }
		public string overview { get; set; }
		public string tvdb_id { get; set; }
	}
}
