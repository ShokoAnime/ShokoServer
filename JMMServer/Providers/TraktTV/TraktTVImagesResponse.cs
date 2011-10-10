using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTVImagesResponse
	{
		public TraktTVImagesResponse() { }

		[DataMember]
		public string poster { get; set; }

		[DataMember]
		public string fanart { get; set; }

		public override string ToString()
		{
			return string.Format("{0} - {1}", poster, fanart);
		}
	}
}
