using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTVEpisode
	{
		public TraktTVEpisode() { }

		[DataMember]
		public string season { get; set; }

		[DataMember]
		public string episode { get; set; }

		[DataMember]
		public string title { get; set; }

		[DataMember]
		public string overview { get; set; }

		[DataMember]
		public string url { get; set; }

		[DataMember]
		public string screen { get; set; } // episode image

		public override string ToString()
		{
			return string.Format("S{0} - EP {1} - {2}", season, episode, title);
		}
	}
}
