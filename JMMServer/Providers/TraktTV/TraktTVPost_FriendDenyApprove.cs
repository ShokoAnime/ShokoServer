using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using JMMServer.Entities;
using NLog;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTVPost_FriendDenyApprove
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		//http://trakt.tv/api-docs/friends-deny
		//http://trakt.tv/api-docs/friends-approve

		[DataMember]
		public string username { get; set; }

		[DataMember]
		public string password { get; set; }

		[DataMember]
		public string friend { get; set; }

		public TraktTVPost_FriendDenyApprove()
		{
		}

		public void Init(string friendUserName)
		{
			username = ServerSettings.Trakt_Username;
			password = Utils.CalculateSHA1(ServerSettings.Trakt_Password, Encoding.Default);
			friend = friendUserName;
		}
	}
}
