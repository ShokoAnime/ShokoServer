using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTV_Shout
	{
		public TraktTV_Shout() { }

		[DataMember]
		public string text { get; set; }

		[DataMember]
		public bool spoiler { get; set; }

		public override string ToString()
		{
			return text;
		}
	}
}
