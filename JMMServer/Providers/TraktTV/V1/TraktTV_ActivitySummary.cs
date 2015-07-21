using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTV_ActivitySummary
	{
		public TraktTV_ActivitySummary() { }

		[DataMember]
		public TraktTV_Timestamps timestamps { get; set; }

		[DataMember]
		public List<TraktTV_Activity> activity { get; set; }

		
	}
}
