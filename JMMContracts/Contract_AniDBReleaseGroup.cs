using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_AniDBReleaseGroup
	{
		public int GroupID { get; set; }
		public string GroupName { get; set; }
		public bool UserCollecting { get; set; }
		public int FileCount { get; set; }
		public string EpisodeRange { get; set; }

		public Contract_AniDBReleaseGroup()
		{
		}
	}
}
