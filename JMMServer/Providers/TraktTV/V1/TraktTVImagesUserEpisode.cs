using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTVImagesUserEpisode
	{
		public TraktTVImagesUserEpisode() { }

		[DataMember]
		public string screen { get; set; }

		public override string ToString()
		{
			return string.Format("{0}", screen);
		}
	}
}
