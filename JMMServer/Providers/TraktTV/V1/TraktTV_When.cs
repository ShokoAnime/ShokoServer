using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTV_When
	{
		public TraktTV_When() { }

		[DataMember]
		public string day { get; set; }

		[DataMember]
		public string time { get; set; }
	}
}
