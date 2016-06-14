using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_AppVersions
	{
		public string JMMServerVersion { get; set; }
		public string JMMServerDownload { get; set; }

		public string JMMDesktopVersion { get; set; }
		public string JMMDesktopDownload { get; set; }

		public string MyAnime3Version { get; set; }
		public string MyAnime3Download { get; set; }

		// default constructor
		public Contract_AppVersions()
		{
		}
	}
}
