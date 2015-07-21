using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using JMMContracts;
using JMMServer.Repositories;
using JMMServer.Entities;
using BinaryNorthwest;


namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTV_UserActivity
	{
		public TraktTV_UserActivity() { }

		[DataMember]
		public string username { get; set; }

		[DataMember(Name="protected")]
		public bool protectedUser { get; set; }

		[DataMember]
		public string full_name { get; set; }

		[DataMember]
		public string gender { get; set; }

		[DataMember]
		public string age { get; set; }

		[DataMember]
		public string location { get; set; }

		[DataMember]
		public string about { get; set; }

		[DataMember]
		public int joined { get; set; }

		[DataMember]
		public string avatar { get; set; }

		[DataMember]
		public string url { get; set; }

		public override string ToString()
		{
			return username;
		}
	}
}
