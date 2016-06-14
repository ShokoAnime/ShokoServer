using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_AnimeRating
	{
		public int AnimeID { get; set; }
		public Contract_AniDB_AnimeDetailed AnimeDetailed { get; set; }
		public Contract_AnimeSeries AnimeSeries { get; set; }
	}
}
