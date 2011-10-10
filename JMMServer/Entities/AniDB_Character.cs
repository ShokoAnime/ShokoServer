using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AniDBAPI;

namespace JMMServer.Entities
{
	public class AniDB_Character
	{
		public int AniDB_CharacterID { get; private set; }
		public int CharID { get; set; }
		public string PicName { get; set; }
		public string CreatorListRaw { get; set; }
		public string CharName { get; set; }
		public string CharKanjiName { get; set; }
		public string CharDescription { get; set; }


		private void Populate(Raw_AniDB_Character rawChar)
		{
			this.CharID = rawChar.CharID;
			this.CharDescription = rawChar.CharDescription;
			this.CharKanjiName = rawChar.CharKanjiName;
			this.CharName = rawChar.CharName;
			this.PicName = rawChar.PicName;
			this.CreatorListRaw = rawChar.CreatorListRaw;
			
		}

		public void PopulateFromHTTP(Raw_AniDB_Character rawChar)
		{
			if (this.CharID == 0) // a new object
			{
				Populate(rawChar);
			}
			else
			{
				// only update the fields that come from HTTP API
				this.CharDescription = rawChar.CharDescription;
				this.CharName = rawChar.CharName;
				this.CreatorListRaw = rawChar.CreatorListRaw;
			}
		}

		public void PopulateFromUDP(Raw_AniDB_Character rawChar)
		{
			if (this.CharID == 0) // a new object
			{
				Populate(rawChar);
			}
			else
			{
				// only update the fields that com from UDP API
				this.CharKanjiName = rawChar.CharKanjiName;
				this.CharName = rawChar.CharName;
				//this.CreatorListRaw = rawChar.CreatorListRaw;
			}
		}
	}
}
