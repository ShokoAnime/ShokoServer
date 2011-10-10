using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using JMMServer.Entities;
using AniDBAPI;
using System.IO;

namespace JMMServer.WebCache
{
	[Serializable]
	[XmlRoot("FileHashRequest")]
	public class FileHashRequest : XMLBase
	{

		protected string fname = "";
		public string Fname
		{
			get { return fname; }
			set { fname = value; }
		}

		protected long fsize = 0;
		public long Fsize
		{
			get { return fsize; }
			set { fsize = value; }
		}

		protected string uname = "";
		public string Uname
		{
			get { return uname; }
			set { uname = value; }
		}

		protected string hash = "";
		public string Hash
		{
			get { return hash; }
			set { hash = value; }
		}

		// default constructor
		public FileHashRequest()
		{
		}

		// default constructor
		public FileHashRequest(VideoLocal data)
		{
			this.fsize = data.FileSize;
			this.hash = data.ED2KHash;
			this.fname = Path.GetFileName(data.FilePath);

			string username = ServerSettings.AniDB_Username;
			if (ServerSettings.WebCache_Anonymous)
				username = Constants.AnonWebCacheUsername;

			this.uname = username;
		}
	}
}
