using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using JMMServer.Entities;
using NLog;


namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTVPost_AccountCreate
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		//http://trakt.tv/api-docs/account-create

		[DataMember]
		public string username { get; set; }

		[DataMember]
		public string password { get; set; }

		[DataMember]
		public string email { get; set; }

		public TraktTVPost_AccountCreate()
		{
		}

		public void Init(string uname, string pass, string emailAddress)
		{
			username = uname;
			password = Utils.CalculateSHA1(pass, Encoding.Default);
			email = emailAddress;
		}
	}
}
