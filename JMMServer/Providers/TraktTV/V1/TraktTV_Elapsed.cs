using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTV_Elapsed
	{
		public TraktTV_Elapsed() { }

		[DataMember]
		public string full { get; set; }

		[DataMember(Name="short")]
		public string shortElapsed { get; set; }
	}
}
