using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMServer.Providers.Azure
{
	public class AnimeIDTitle
	{
		public int AnimeIDTitleId { get; set; }
		public int AnimeID { get; set; }
		public string MainTitle { get; set; }
		public string Titles { get; set; }

		public override string ToString()
		{
			return string.Format("{0} - {1} - {2}", AnimeID, MainTitle, Titles);
		}
	}
}
