using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMServer.Providers.Azure
{
	public class AnimeXML
	{
		public int AnimeXMLId { get; set; }
		public int AnimeID { get; set; }
		public string AnimeName { get; set; }
		public string Username { get; set; }
		public long DateDownloaded { get; set; }
		public string XMLContent { get; set; }
	}
}
