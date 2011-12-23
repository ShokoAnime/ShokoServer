using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTV_Timestamps
	{
		public TraktTV_Timestamps() { }

		[DataMember]
		public int start { get; set; }

		[DataMember]
		public int current { get; set; }
	}
}
