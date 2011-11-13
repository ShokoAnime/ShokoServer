using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTVSeason
	{
		public TraktTVSeason() { }

		[DataMember]
		public string season { get; set; }

		[DataMember]
		public string url { get; set; }

		[DataMember]
		public TraktTVImages images { get; set; }

		[DataMember]
		public List<TraktTVEpisode> episodes { get; set; }

		public override string ToString()
		{
			return string.Format("S{0} - {1} episodes", season, episodes.Count);
		}
	}
}
