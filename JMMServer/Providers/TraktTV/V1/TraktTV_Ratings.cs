using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTV_Ratings
	{
		public TraktTV_Ratings() { }

		[DataMember]
		public int percentage { get; set; }

		[DataMember]
		public int votes { get; set; }

		[DataMember]
		public int loved { get; set; }

		[DataMember]
		public int hated { get; set; }
	}
}
