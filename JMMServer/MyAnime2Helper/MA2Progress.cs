using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMServer.MyAnime2Helper
{
	public class MA2Progress
	{
		public int TotalFiles { get; set; }
		public int CurrentFile { get; set; }
		public int MigratedFiles { get; set; }
		public string ErrorMessage { get; set; }
	}
}
