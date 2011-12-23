using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_Trakt_FriendActivity
	{
		// user details
		public int Trakt_FriendID { get; set; }
		public string Username { get; set; }
		public string Full_name { get; set; }
		public string Gender { get; set; }
		public object Age { get; set; }
		public string Location { get; set; }
		public string About { get; set; }
		public int Joined { get; set; }
		public DateTime? JoinedDate { get; set; }
		public string Avatar { get; set; }
		public string Url { get; set; }

		// activity details
		public int ActivityAction { get; set; } // scrobble, shout
		public int ActivityType { get; set; } // episode, show
		public DateTime? ActivityDate { get; set; }
		public string Elapsed { get; set; }
		public string ElapsedShort { get; set; }

		// if episode
		public  Contract_Trakt_WatchedEpisode Episode { get; set; }

		// if shout
		public Contract_Trakt_Shout Shout { get; set; }
	}
}
