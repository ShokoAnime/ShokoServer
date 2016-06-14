using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_ReleaseGroup
	{
		public int GroupID { get; set; }
		public int Rating { get; set; }
		public int Votes { get; set; }
		public int AnimeCount { get; set; }
		public int FileCount { get; set; }
		public string GroupName { get; set; }
		public string GroupNameShort { get; set; }
		public string IRCChannel { get; set; }
		public string IRCServer { get; set; }
		public string URL { get; set; }
		public string Picname { get; set; }
	}
}
