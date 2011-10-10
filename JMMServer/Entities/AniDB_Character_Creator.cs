using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMServer.Entities
{
	public class AniDB_Character_Creator
	{
		public int AniDB_Character_CreatorID { get; private set; }
		public int CharID { get; set; }
		public int CreatorID { get; set; }

		public AniDB_Character_Creator()
		{
		}
	}
}
