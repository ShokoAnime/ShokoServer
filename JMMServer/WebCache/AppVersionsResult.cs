using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMServer.WebCache
{
	public class AppVersionsResult
	{
		public string JMMServerVersion { get; set; }
		public string JMMServerDownload { get; set; }

		public string JMMDesktopVersion { get; set; }
		public string JMMDesktopDownload { get; set; }

		public string MyAnime3Version { get; set; }
		public string MyAnime3Download { get; set; }

		// default constructor
		public AppVersionsResult()
		{
		}
	}
}
