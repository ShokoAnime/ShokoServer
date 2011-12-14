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
	public class TraktTVPost_GetFriends
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		[DataMember]
		public string username { get; set; }

		[DataMember]
		public string password { get; set; }

		public TraktTVPost_GetFriends()
		{
		}

		public void Init()
		{
			username = ServerSettings.Trakt_Username;
			password = Utils.CalculateSHA1(ServerSettings.Trakt_Password, Encoding.Default);
		}
	}
}
