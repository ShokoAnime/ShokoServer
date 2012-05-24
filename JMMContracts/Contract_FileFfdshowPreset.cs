using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_FileFfdshowPreset
	{
		public int? FileFfdshowPresetID { get; set; }
		public string Hash { get; set; }
		public long FileSize { get; set; }
		public string Preset { get; set; }
	}
}
