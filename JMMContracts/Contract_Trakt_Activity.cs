using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_Trakt_Activity
	{
		public bool HasTraktAccount { get; set; }
		public List<Contract_Trakt_Friend> TraktFriends { get; set; }
		public List<Contract_Trakt_FriendFrequest> TraktFriendRequests { get; set; }
		public List<Contract_Trakt_FriendActivity> TraktFriendActivity { get; set; }
	}
}
