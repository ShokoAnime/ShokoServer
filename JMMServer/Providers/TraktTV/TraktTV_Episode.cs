using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTV_Episode
	{
		public TraktTV_Episode() { }

		[DataMember]
		public string season { get; set; }

		[DataMember]
		public int episode { get; set; }

		[DataMember]
		public int first_aired { get; set; }

		[DataMember]
		public string number { get; set; }

		[DataMember]
		public string title { get; set; }

		[DataMember]
		public string overview { get; set; }

		[DataMember]
		public string url { get; set; }

		[DataMember]
		public TraktTVImagesUserEpisode images { get; set; }

		[DataMember]
		public TraktTV_Ratings ratings { get; set; }

		public override string ToString()
		{
			return string.Format("S{0} - EP {1} - {2}", season, number, title);
		}

    
    
	}
}
