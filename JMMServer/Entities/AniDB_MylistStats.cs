using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AniDBAPI;
using JMMServer.AniDB_API.Raws;

namespace JMMServer.Entities
{
	public class AniDB_MylistStats
	{
		public int AniDB_MylistStatsID { get; private set; }
		public int Animes { get; set; }
		public int Episodes { get; set; }
		public int Files { get; set; }
		public long SizeOfFiles { get; set; }
		public int AddedAnimes { get; set; }
		public int AddedEpisodes { get; set; }
		public int AddedFiles { get; set; }
		public int AddedGroups { get; set; }
		public int LeechPct { get; set; }
		public int GloryPct { get; set; }
		public int ViewedPct { get; set; }
		public int MylistPct { get; set; }
		public int ViewedMylistPct { get; set; }
		public int EpisodesViewed { get; set; }
		public int Votes { get; set; }
		public int Reviews { get; set; }
		public int ViewiedLength { get; set; }

		public AniDB_MylistStats()
		{
		}

		public void Populate(Raw_AniDB_MyListStats raw)
		{
			this.Animes = raw.Animes;
			this.Episodes = raw.Episodes;
			this.Files = raw.Files;
			this.SizeOfFiles = raw.SizeOfFiles;
			this.AddedAnimes = raw.AddedAnimes;
			this.AddedEpisodes = raw.AddedEpisodes;
			this.AddedFiles = raw.AddedFiles;
			this.AddedGroups = raw.AddedGroups;
			this.LeechPct = raw.LeechPct;
			this.GloryPct = raw.GloryPct;
			this.ViewedPct = raw.ViewedPct;
			this.MylistPct = raw.MylistPct;
			this.ViewedMylistPct = raw.ViewedMylistPct;
			this.EpisodesViewed = raw.EpisodesViewed;
			this.Votes = raw.Votes;
			this.Reviews = raw.Reviews;
			this.ViewiedLength = raw.ViewiedLength;
		}

		public AniDB_MylistStats(Raw_AniDB_MyListStats raw)
		{
			Populate(raw);

		}

		public override string ToString()
		{
			return string.Format("AniDB_MylistStats:: Animes: {0} | Episodes: {1} | Files: {2}", Animes, Episodes, Files);
		}
	}
}
