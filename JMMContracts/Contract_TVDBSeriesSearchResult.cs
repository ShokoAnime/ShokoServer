using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_TVDBSeriesSearchResult
	{
		public string Id { get; set; }
		public int SeriesID { get; set; }
		public string Overview { get; set; }
		public string SeriesName { get; set; }
		public string Banner { get; set; }
		public string Language { get; set; }
	}
}
