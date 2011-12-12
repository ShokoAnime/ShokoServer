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
	public class TraktTVPost_FriendsRequests
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		//http://trakt.tv/api-docs/friends-requests

		[DataMember]
		public string username { get; set; }

		[DataMember]
		public string password { get; set; }

		public TraktTVPost_FriendsRequests()
		{
		}

		public void Init()
		{
			username = ServerSettings.Trakt_Username;
			password = Utils.CalculateSHA1(ServerSettings.Trakt_Password, Encoding.Default);
		}
	}
}
