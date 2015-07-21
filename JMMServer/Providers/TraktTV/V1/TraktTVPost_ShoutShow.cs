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
	public class TraktTVPost_ShoutShow
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		//http://trakt.tv/api-docs/account-create

		[DataMember]
		public string username { get; set; }

		[DataMember]
		public string password { get; set; }

		[DataMember]
		public int tvdb_id { get; set; }

		[DataMember]
		public string shout { get; set; }

		[DataMember]
		public bool spoiler { get; set; }

		public TraktTVPost_ShoutShow()
		{
		}

		public void Init(string shoutText, bool isSpoiler, int tvDBID)
		{
			//username = ServerSettings.Trakt_Username;
			//password = Utils.CalculateSHA1(ServerSettings.Trakt_Password, Encoding.Default);

			tvdb_id = tvDBID;
			shout = shoutText;
			spoiler = isSpoiler;

		}
	}
}
