using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class MetroContract_Shout
	{
		public string Source { get; set; } // trakt, anidb
		public int ShoutType { get; set; } // trakt - shout, anidb (for fans, must see, Recommended)
		public string ShoutText { get; set; }
		public bool IsSpoiler { get; set; }
		public DateTime? ShoutDate { get; set; }
		public int UserID { get; set; }
		public string UserName { get; set; }
		public string ImageURL { get; set; }
	}
}
