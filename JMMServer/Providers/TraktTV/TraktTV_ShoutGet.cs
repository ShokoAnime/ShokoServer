using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTV_ShoutGet
	{
		public TraktTV_ShoutGet() { }

		[DataMember]
		public int inserted { get; set; }

		[DataMember]
		public string shout { get; set; }

		[DataMember]
		public bool spoiler { get; set; }

		[DataMember]
		public TraktTV_UserActivity user { get; set; }

		public override string ToString()
		{
			return shout;
		}
	}
}
