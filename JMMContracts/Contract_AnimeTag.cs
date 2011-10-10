using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_AnimeTag
	{
		public int TagID { get; set; }
		public int Spoiler { get; set; }
		public int LocalSpoiler { get; set; }
		public int GlobalSpoiler { get; set; }
		public string TagName { get; set; }
		public int TagCount { get; set; }
		public string TagDescription { get; set; }
		public int Approval { get; set; }
	}
}
