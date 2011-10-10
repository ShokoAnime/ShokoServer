using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using JMMServer.Entities;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTVPost_ShowWatching
	{
		// http://trakt.tv/api-docs/show-watching

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
		public string season { get; set; }

		[DataMember]
		public string episode { get; set; }

		[DataMember]
		public string duration { get; set; }

		[DataMember]
		public string progress { get; set; }

		[DataMember]
		public string plugin_version { get; set; }

		[DataMember]
		public string media_center_version { get; set; }

		[DataMember]
		public string media_center_date { get; set; }

		public TraktTVPost_ShowWatching()
		{
		}

		public void Init(AnimeEpisode episode)
		{
			username = ServerSettings.Trakt_Username;
			password = Utils.CalculateSHA1(ServerSettings.Trakt_Password, Encoding.Default);


		}
	}
}
