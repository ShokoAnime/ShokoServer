using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTVSeasonUserCollectionWatched
	{
		public TraktTVSeasonUserCollectionWatched() { }

		[DataMember]
		public string season { get; set; }

		[DataMember]
		public List<string> episodes { get; set; }

		public List<int> EpisodeNumbers
		{
			get
			{
				List<int> eps = new List<int>();
				foreach (string sep in episodes)
				{
					int epnum = 0;
					int.TryParse(sep, out epnum);
					if (epnum > 0) eps.Add(epnum);
				}
				return eps;
			}
		}

		public override string ToString()
		{
			return string.Format("S{0} - {1} episodes", season, episodes.Count);
		}
	}
}
