using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_GroupFileSummary
	{
		public string GroupName { get; set; }
		public string GroupNameShort { get; set; }
		public int FileCountNormal { get; set; }
		public bool NormalComplete { get; set; }
		public int FileCountSpecials { get; set; }
		public bool SpecialsComplete { get; set; }

		public List<int> NormalEpisodeNumbers { get; set; }
		public string NormalEpisodeNumberSummary { get; set; }

		public override string ToString()
		{
			return string.Format("{0} - {1}/{2}", GroupNameShort, FileCountNormal, FileCountSpecials);
		}
	}
}
