using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTVWatched
	{
		public TraktTVWatched() { }

		[DataMember]
		public string type { get; set; }

		[DataMember]
		public int watched { get; set; }

		[DataMember]
		public TraktTVShow show { get; set; }

		[DataMember]
		public TraktTVEpisodeUser episode { get; set; }
		//public Movie movie { get; set; }

		public override string ToString()
		{
			return string.Format("{0} - {1}", type, watched);
		}

	}
}
