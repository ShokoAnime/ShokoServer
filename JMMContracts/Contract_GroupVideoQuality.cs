using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_GroupVideoQuality : IComparable<Contract_GroupVideoQuality>
	{
		public string GroupName { get; set; }
		public string GroupNameShort { get; set; }
		public int Ranking { get; set; }
		public string Resolution { get; set; }
		public string VideoSource { get; set; }
		public int VideoBitDepth { get; set; }
		public int FileCountNormal { get; set; }
		public bool NormalComplete { get; set; }
		public int FileCountSpecials { get; set; }
		public bool SpecialsComplete { get; set; }

		public List<int> NormalEpisodeNumbers { get; set; }
		public string NormalEpisodeNumberSummary { get; set; }

		public int CompareTo(Contract_GroupVideoQuality obj)
		{
			return Ranking.CompareTo(obj.Ranking) * -1;
		}

		public override string ToString()
		{
			return string.Format("{0} - {1}/{2}", GroupNameShort, Resolution, VideoSource);
		}
	}
}
