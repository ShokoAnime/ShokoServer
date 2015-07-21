using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTVShowUserCollectionWatched
	{
		public TraktTVShowUserCollectionWatched() { }

		[DataMember]
		public string title { get; set; }

		[DataMember]
		public string year { get; set; }

		[DataMember]
		public string url { get; set; }

		[DataMember]
		public string first_aired { get; set; }

		[DataMember]
		public string country { get; set; }

		[DataMember]
		public string overview { get; set; }

		[DataMember]
		public string tvdb_id { get; set; }

		[DataMember]
		public TraktTVImages images { get; set; }

		[DataMember]
		public List<TraktTVSeasonUserCollectionWatched> seasons { get; set; }

		public string TraktID
		{
			get
			{
				if (string.IsNullOrEmpty(url)) return "";

				int pos = url.LastIndexOf("/");
				if (pos < 0) return "";

				string id = url.Substring(pos + 1, url.Length - pos - 1);
				return id;
			}
		}

		public override string ToString()
		{
			return string.Format("{0} - {1} - {2}", title, year, overview);
		}
	}
}
