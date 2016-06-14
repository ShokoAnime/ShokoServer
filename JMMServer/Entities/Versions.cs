using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMServer.Entities
{
	public class Versions
	{
		public int VersionsID { get; private set; }
		public string VersionType { get; set; }
		public string VersionValue { get; set; }
	}
}
