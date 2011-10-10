using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using JMMServer.Entities;
using NLog;
using JMMServer.Repositories;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTVPost_ShowEpisodeSeen
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		// http://trakt.tv/api-docs/show-episode-seen

		[DataMember]
		public string username { get; set; }

		[DataMember]
		public string password { get; set; }

		[DataMember]
		public string imdb_id { get; set; }

		[DataMember]
		public string tvdb_id { get; set; }

		[DataMember]
		public string title { get; set; }

		[DataMember]
		public string year { get; set; }

		[DataMember]
		public List<TraktTVSeasonEpisode> episodes { get; set; }

		public void SetCredentials()
		{
			username = ServerSettings.Trakt_Username;
			password = Utils.CalculateSHA1(ServerSettings.Trakt_Password, Encoding.Default);
		}
	}
}
