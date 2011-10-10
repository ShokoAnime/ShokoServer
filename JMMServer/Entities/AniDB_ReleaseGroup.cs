using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AniDBAPI;
using JMMContracts;

namespace JMMServer.Entities
{
	public class AniDB_ReleaseGroup
	{
		public int AniDB_ReleaseGroupID { get; private set; }
		public int GroupID { get; set; }
		public int Rating { get; set; }
		public int Votes { get; set; }
		public int AnimeCount { get; set; }
		public int FileCount { get; set; }
		public string GroupName { get; set; }
		public string GroupNameShort { get; set; }
		public string IRCChannel { get; set; }
		public string IRCServer { get; set; }
		public string URL { get; set; }
		public string Picname { get; set; }

		public AniDB_ReleaseGroup()
		{
		}

		public AniDB_ReleaseGroup(Raw_AniDB_Group raw)
		{
			Populate(raw);
		}

		public void Populate(Raw_AniDB_Group raw)
		{
			this.GroupID = raw.GroupID;
			this.Rating = raw.Rating;
			this.Votes = raw.Votes;
			this.AnimeCount = raw.AnimeCount;
			this.FileCount = raw.FileCount;
			this.GroupName = raw.GroupName;
			this.GroupNameShort = raw.GroupNameShort;
			this.IRCChannel = raw.IRCChannel;
			this.IRCServer = raw.IRCServer;
			this.URL = raw.URL;
			this.Picname = raw.Picname;
		}

		public Contract_ReleaseGroup ToContract()
		{
			Contract_ReleaseGroup contract = new Contract_ReleaseGroup();

			contract.GroupID = this.GroupID;
			contract.Rating = this.Rating;
			contract.Votes = this.Votes;
			contract.AnimeCount = this.AnimeCount;
			contract.FileCount = this.FileCount;
			contract.GroupName = this.GroupName;
			contract.GroupNameShort = this.GroupNameShort;
			contract.IRCChannel = this.IRCChannel;
			contract.IRCServer = this.IRCServer;
			contract.URL = this.URL;
			contract.Picname = this.Picname;

			return contract;
		}

		public override string ToString()
		{
			return string.Format("Release Group: {0} - {1} : {2}", GroupID, GroupName, URL);
		}
	}
}
