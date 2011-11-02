using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_AniDB_Creator
	{
		public int AniDB_CreatorID { get; set; }
		public int CreatorID { get; set; }
		public string CreatorName { get; set; }
		public string PicName { get; set; }
		public string CreatorKanjiName { get; set; }
		public string CreatorDescription { get; set; }
		public int CreatorType { get; set; }
		public string URLEnglish { get; set; }
		public string URLJapanese { get; set; }
		public string URLWikiEnglish { get; set; }
		public string URLWikiJapanese { get; set; }
	}
}
