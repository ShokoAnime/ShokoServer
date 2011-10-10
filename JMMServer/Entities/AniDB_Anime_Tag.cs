using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AniDBAPI;

namespace JMMServer.Entities
{
	public class AniDB_Anime_Tag
	{
		public int AniDB_Anime_TagID { get; private set; }
		public int AnimeID { get; set; }
		public int TagID { get; set; }
		public int Approval { get; set; }

		public void Populate(Raw_AniDB_Tag rawTag)
		{
			this.AnimeID = rawTag.AnimeID;
			this.TagID = rawTag.TagID;
			this.Approval = rawTag.Approval;
		}
	}
}
